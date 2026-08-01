using TobbeSQL.Parser.Ast;
using TobbeSQL.Storage;

namespace TobbeSQL.Execution;

public class QueryExecutor(Catalog catalog)
{
    public QueryResult Execute(Statement statement)
    {
        return statement switch
        {
            CreateTableStatement stmt => ExecuteCreateTable(stmt),
            DropTableStatement stmt => ExecuteDropTable(stmt),
            CountStatement stmt => ExecuteCount(stmt),
            InsertStatement stmt => ExecuteInsert(stmt),
            SelectStatement stmt => ExecuteSelect(stmt),
            DeleteStatement stmt => ExecuteDelete(stmt),
            CreateIndexStatement stmt => ExecuteCreateIndex(stmt),
            DropIndexStatement stmt => ExecuteDropIndex(stmt),
            _ => throw new Exception($"Unknown statement type: {statement.GetType()}"),
        };
    }

    private QueryResult ExecuteCreateTable(CreateTableStatement stmt)
    {
        catalog.CreateTable(new Schema(stmt.TableName, stmt.Columns));
        return new QueryResult { Message = $"Table created: {stmt.TableName}" };
    }

    private QueryResult ExecuteDropTable(DropTableStatement stmt)
    {
        catalog.DropTable(stmt.TableName);
        return new QueryResult { Message = $"Table dropped: {stmt.TableName}" };
    }

    private QueryResult ExecuteCount(CountStatement stmt)
    {
        var (schema, dataFilePath) = catalog.GetTable(stmt.TableName);
        var predicate = stmt.WhereClause is not null
            ? ExpressionEvaluator.Compile(stmt.WhereClause, schema)
            : null;
        using var pageManager = new PageManager(dataFilePath);
        var heapFile = new HeapFile(pageManager);

        var serializer = new RowSerializer();
        var counter = 0;
        foreach (var (rowId, data) in heapFile.Scan())
        {
            if (predicate is null || predicate(serializer.Deserialize(schema, data)))
            {
                counter++;
            }
        }

        return new QueryResult
        {
            Columns = ["count"],
            Rows =
            [
                [counter],
            ],
        };
    }

    private QueryResult ExecuteInsert(InsertStatement stmt)
    {
        var (schema, dataFilePath) = catalog.GetTable(stmt.TableName);
        using var pageManager = new PageManager(dataFilePath);
        var heapFile = new HeapFile(pageManager);

        var indexedColumns = schema
            .Columns.Select(
                (c, i) => (Index: i, IndexMetadata: catalog.GetIndex(schema.TableName, c.Name))
            )
            .Where(x => x.IndexMetadata is not null)
            .Select(x =>
                (
                    x.Index,
                    PageManager: new PageManager(x.IndexMetadata!.Value.DataFilePath),
                    x.IndexMetadata.Value.Unique
                )
            )
            .ToList();

        var sortedValues = SortValues(stmt, schema).ToList();
        var serializer = new RowSerializer();
        var rowIds = heapFile
            .InsertBatch(sortedValues.Select(s => serializer.Serialize(schema, s)))
            .ToList();

        foreach (var (colIdx, indexPm, unique) in indexedColumns)
        {
            for (var i = 0; i < sortedValues.Count; i++)
            {
                var values = sortedValues[i];
                var rowId = rowIds[i];
                var tree = new BTree(indexPm);
                tree.Insert((int)values[colIdx], rowId, unique);
            }
        }

        foreach (var indexedColumn in indexedColumns)
        {
            indexedColumn.PageManager.Dispose();
        }

        return new QueryResult { AffectedRows = stmt.Values.Count };
    }

    private IEnumerable<object[]> SortValues(InsertStatement stmt, Schema schema)
    {
        var stmtIndexToColumnIndex = new Dictionary<int, int>();
        var mappingExceptions = new List<Exception>();
        for (var i = 0; i < stmt.Columns.Count; i++)
        {
            var stmtColumn = stmt.Columns[i];
            var schemaIndex = schema.Columns.FindIndex(s => s.Name == stmtColumn);
            if (schemaIndex == -1)
            {
                mappingExceptions.Add(
                    new Exception($"Could not find column at insert: {stmtColumn}")
                );
            }
            stmtIndexToColumnIndex[schemaIndex] = i;
        }

        if (mappingExceptions.Count != 0)
        {
            throw new AggregateException(mappingExceptions);
        }

        foreach (var valueList in stmt.Values)
        {
            var values = new object[schema.Columns.Count];
            for (var i = 0; i < valueList.Count; i++)
            {
                values[i] = valueList[stmtIndexToColumnIndex[i]];
            }

            yield return values;
        }
    }

    private QueryResult ExecuteSelect(SelectStatement stmt)
    {
        var (schema, _) = catalog.GetTable(stmt.TableName);
        var columns =
            stmt.Columns[0] == "*" ? schema.Columns.Select(c => c.Name).ToList() : stmt.Columns;

        var rowFinder = new RowFinder(catalog);
        return new QueryResult
        {
            Columns = columns,
            Rows = rowFinder.FindRows(stmt.TableName, columns, stmt.WhereClause).ToList(),
        };
    }

    private QueryResult ExecuteDelete(DeleteStatement stmt)
    {
        var (schema, dataFilePath) = catalog.GetTable(stmt.TableName);
        var indexedColumns = schema
            .Columns.Select(
                (c, i) => (Index: i, IndexMetadata: catalog.GetIndex(schema.TableName, c.Name))
            )
            .Where(x => x.IndexMetadata is not null)
            .Select(x =>
                (x.Index, PageManager: new PageManager(x.IndexMetadata!.Value.DataFilePath))
            )
            .ToList();
        var predicate = stmt.WhereClause is not null
            ? ExpressionEvaluator.Compile(stmt.WhereClause, schema)
            : (_) => true;
        using var pageManager = new PageManager(dataFilePath);
        var heapFile = new HeapFile(pageManager);
        var serializer = new RowSerializer();
        var result = new QueryResult();
        foreach (var (rowId, data) in heapFile.Scan())
        {
            object[]? values = null;
            object[] GetValues() => values ??= serializer.Deserialize(schema, data);

            if (stmt.WhereClause is not null && !predicate(GetValues()))
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
        var (schema, tableDataFilePath) = catalog.GetTable(stmt.TableName);
        var columnIndex = schema.Columns.FindIndex(c => c.Name == stmt.ColumnName);
        if (columnIndex == -1)
        {
            throw new Exception($"Column does not exist: {stmt.ColumnName}");
        }
        if (schema.Columns[columnIndex].Type != ColumnType.Integer)
        {
            throw new Exception("Only support indexes on integer type columns");
        }

        var indexDataFilePath = catalog.CreateIndex(
            stmt.IndexName,
            stmt.TableName,
            stmt.ColumnName,
            stmt.Unique
        );

        using var indexPageManager = new PageManager(indexDataFilePath);
        var tree = BTree.Create(indexPageManager);

        using var tablePageManager = new PageManager(tableDataFilePath);
        var heapFile = new HeapFile(tablePageManager);
        var serializer = new RowSerializer();

        try
        {
            foreach (var (rowId, data) in heapFile.Scan())
            {
                var row = serializer.Deserialize(schema, data);
                tree.Insert((int)row[columnIndex], rowId, stmt.Unique);
            }
        }
        catch
        {
            indexPageManager.Dispose();
            catalog.DropIndex(stmt.IndexName);
        }

        return new QueryResult { Message = $"Index created: {stmt.IndexName}" };
    }

    public QueryResult ExecuteDropIndex(DropIndexStatement stmt)
    {
        catalog.DropIndex(stmt.IndexName);
        return new QueryResult { Message = $"Index dropped: {stmt.IndexName}" };
    }
}

internal class RowFinder(Catalog catalog)
{
    public IEnumerable<object[]> FindRows(
        string tableName,
        List<string> columns,
        Expression? whereExpression
    )
    {
        var (schema, dataFilePath) = catalog.GetTable(tableName);
        using var pageManager = new PageManager(dataFilePath);
        var heapFile = new HeapFile(pageManager);
        var serializer = new RowSerializer();

        var columnIndices = columns
            .Select(name => schema.Columns.FindIndex(c => c.Name == name))
            .ToArray();

        object[] Project(object[] values) => [.. columnIndices.Select(i => values[i])];

        var predicate = whereExpression is not null
            ? ExpressionEvaluator.Compile(whereExpression, schema)
            : (_) => true;

        if (whereExpression is not null)
        {
            var (MetaData, Value) = columns
                .Select(c =>
                    (
                        MetaData: catalog.GetIndex(schema.TableName, c),
                        Value: ExpressionEvaluator.IndexComparison(whereExpression, c)
                    )
                )
                .FirstOrDefault(x => x.MetaData is not null && x.Value is not null);

            if (MetaData is not null)
            {
                using var indexPM = new PageManager(MetaData.Value.DataFilePath);
                var tree = new BTree(indexPM);
                foreach (var rowId in tree.Search((int)Value!))
                {
                    var data = heapFile.GetRow(rowId);
                    yield return Project(serializer.Deserialize(schema, data!));
                }
                yield break;
            }
        }

        foreach (var (rowId, data) in heapFile.Scan())
        {
            var values = serializer.Deserialize(schema, data);
            if (whereExpression is not null && !predicate(values))
            {
                continue;
            }

            yield return Project(values);
        }
    }
}
