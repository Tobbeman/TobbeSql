using TobbeSQL.Parser.Ast;
using TobbeSQL.Storage;

namespace TobbeSQL.Execution;

public class QueryExecutor
{
    private readonly Catalog _catalog;

    public QueryExecutor(Catalog catalog)
    {
        _catalog = catalog;
    }

    public QueryResult Execute(Statement statement)
    {
        return statement switch
        {
            CreateTableStatement stmt => ExecuteCreateTable(stmt),
            InsertStatement stmt => ExecuteInsert(stmt),
            SelectStatement stmt => ExecuteSelect(stmt),
            DeleteStatement stmt => ExecuteDelete(stmt),
            CreateIndexStatement stmt => ExecuteCreateIndex(stmt),
            _ => throw new Exception($"Unknown statement type: {statement.GetType()}"),
        };
    }

    private QueryResult ExecuteCreateTable(CreateTableStatement stmt)
    {
        _catalog.CreateTable(new Schema(stmt.TableName, stmt.Columns));
        return new QueryResult { Message = $"Table created: {stmt.TableName}" };
    }

    private QueryResult ExecuteInsert(InsertStatement stmt)
    {
        var (schema, dataFilePath) = _catalog.GetTable(stmt.TableName);
        using var pageManager = new PageManager(dataFilePath);
        var heapFile = new HeapFile(pageManager);

        var indexedColumns = schema
            .Columns.Select(
                (c, i) => (Index: i, DataFile: _catalog.GetIndex(schema.TableName, c.Name))
            )
            .Where(x => x.DataFile is not null)
            .Select(x => (x.Index, PageManager: new PageManager(x.DataFile!)))
            .ToList();

        foreach (var valueList in stmt.Values)
        {
            var values = new object[schema.Columns.Count];
            for (var i = 0; i < schema.Columns.Count; i++)
            {
                var tableColumn = schema.Columns[i];
                var stmtIndex = stmt.Columns.FindIndex(s => s == tableColumn.Name);
                if (stmtIndex == -1)
                {
                    throw new Exception($"Could not find column at insert: {tableColumn.Name}");
                }

                values[i] = valueList[stmtIndex];
            }
            var serialized = new RowSerializer().Serialize(schema, values);
            var rowId = heapFile.Insert(serialized);

            foreach (var (colIdx, indexPm) in indexedColumns)
            {
                var tree = new BTree(indexPm);
                tree.Insert((int)values[colIdx], rowId);
            }
        }

        foreach (var indexedColumn in indexedColumns)
        {
            indexedColumn.PageManager.Dispose();
        }

        return new QueryResult { AffectedRows = stmt.Values.Count };
    }

    private QueryResult ExecuteSelect(SelectStatement stmt)
    {
        var (schema, dataFilePath) = _catalog.GetTable(stmt.TableName);
        using var pageManager = new PageManager(dataFilePath);
        var heapFile = new HeapFile(pageManager);
        var serializer = new RowSerializer();

        var selectAll = stmt.Columns[0] == "*";

        var indexedColumns = schema
            .Columns.Where(c => stmt.WhereClause is not null)
            .Select(c =>
                (
                    c.Name,
                    DataFile: _catalog.GetIndex(schema.TableName, c.Name),
                    Value: ExpressionEvaluator.IndexComparison(stmt.WhereClause!, c.Name)
                )
            )
            .Where(x => x.DataFile is not null)
            .Where(x => x.Value is not null)
            .Where(x => selectAll || stmt.Columns.Any(c => c == x.Name))
            .Select(x => (x.Name, new PageManager(x.DataFile!), (int)x.Value!))
            .ToList();

        var result = new QueryResult
        {
            Columns = selectAll ? [.. schema.Columns.Select(c => c.Name)] : stmt.Columns,
        };

        if (indexedColumns.Count != 0)
        {
            var (name, indexPM, key) = indexedColumns.First();
            var tree = new BTree(indexPM);
            foreach (var rowId in tree.Search(key))
            {
                var data = heapFile.GetRow(rowId);
                var values = serializer.Deserialize(schema, data!);
                result.Rows.Add(values);
            }

            return result;
        }

        foreach (var (rowId, data) in heapFile.Scan())
        {
            var values = serializer.Deserialize(schema, data);
            if (
                stmt.WhereClause is not null
                && !ExpressionEvaluator.Evaluate(stmt.WhereClause, schema, values)
            )
            {
                continue;
            }

            if (!selectAll)
            {
                var filteredValues = new object[stmt.Columns.Count];
                for (var i = 0; i < stmt.Columns.Count; i++)
                {
                    var columnIndex = schema.Columns.FindIndex(c => c.Name == stmt.Columns[i]);
                    filteredValues[i] = values[columnIndex];
                }
                values = filteredValues;
            }
            result.Rows.Add(values);
        }
        return result;
    }

    private QueryResult ExecuteDelete(DeleteStatement stmt)
    {
        var (schema, dataFilePath) = _catalog.GetTable(stmt.TableName);
        var indexedColumns = schema
            .Columns.Select(
                (c, i) => (Index: i, DataFile: _catalog.GetIndex(schema.TableName, c.Name))
            )
            .Where(x => x.DataFile is not null)
            .Select(x => (x.Index, PageManager: new PageManager(x.DataFile!)))
            .ToList();
        using var pageManager = new PageManager(dataFilePath);
        var heapFile = new HeapFile(pageManager);
        var serializer = new RowSerializer();
        var result = new QueryResult();
        foreach (var (rowId, data) in heapFile.Scan())
        {
            object[]? values = null;
            object[] GetValues() => values ??= serializer.Deserialize(schema, data);

            if (
                stmt.WhereClause is not null
                && !ExpressionEvaluator.Evaluate(stmt.WhereClause, schema, GetValues())
            )
            {
                continue;
            }

            result.AffectedRows++;
            heapFile.Delete(rowId);

            foreach (var indexedColumn in indexedColumns)
            {
                var tree = new BTree(indexedColumn.PageManager);
                tree.Delete((int)GetValues()[indexedColumn.Index], rowId);
            }
        }

        foreach (var indexedColumn in indexedColumns)
        {
            indexedColumn.PageManager.Dispose();
        }

        return result;
    }

    private QueryResult ExecuteCreateIndex(CreateIndexStatement stmt)
    {
        var (schema, tableDataFilePath) = _catalog.GetTable(stmt.TableName);
        var columnIndex = schema.Columns.FindIndex(c => c.Name == stmt.ColumnName);
        if (columnIndex == -1)
        {
            throw new Exception($"Column does not exist: {stmt.ColumnName}");
        }
        if (schema.Columns[columnIndex].Type != ColumnType.Integer)
        {
            throw new Exception("Only support indexes on integer type columns");
        }

        var indexDataFilePath = _catalog.CreateIndex(
            stmt.IndexName,
            stmt.TableName,
            stmt.ColumnName
        );
        using var indexPageManager = new PageManager(indexDataFilePath);
        var tree = BTree.Create(indexPageManager);

        using var tablePageManager = new PageManager(tableDataFilePath);
        var heapFile = new HeapFile(tablePageManager);
        var serializer = new RowSerializer();

        foreach (var (rowId, data) in heapFile.Scan())
        {
            var row = serializer.Deserialize(schema, data);
            tree.Insert((int)row[columnIndex], rowId);
        }

        return new QueryResult { Message = $"Index created: {stmt.IndexName}" };
    }
}
