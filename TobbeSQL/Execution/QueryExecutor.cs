using TobbeSQL.Parser.Ast;
using TobbeSQL.Storage;

namespace TobbeSQL.Execution;

/// <summary>
/// Executes parsed SQL statements by coordinating the storage layer components.
///
/// The executor receives an AST node from the parser and performs the corresponding
/// operations using the Catalog, HeapFile, and RowSerializer.
/// </summary>
public class QueryExecutor
{
    private readonly Catalog _catalog;

    /// <summary>
    /// Creates a QueryExecutor that uses the given Catalog to find tables and their files.
    /// </summary>
    public QueryExecutor(Catalog catalog)
    {
        _catalog = catalog;
    }

    /// <summary>
    /// Executes a parsed SQL statement and returns the result.
    ///
    /// Dispatch to the appropriate private method based on the statement type:
    ///   - CreateTableStatement → ExecuteCreateTable
    ///   - InsertStatement → ExecuteInsert
    ///   - SelectStatement → ExecuteSelect
    ///   - DeleteStatement → ExecuteDelete
    ///   - CreateIndexStatement → ExecuteCreateIndex
    ///
    /// If the statement type is unknown, throw an exception.
    /// </summary>
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

    /// <summary>
    /// Executes CREATE TABLE.
    ///
    /// Steps:
    ///   1. Build a Schema from the statement's TableName and Columns
    ///   2. Call _catalog.CreateTable(schema)
    ///   3. Return a QueryResult with a success Message (e.g. "Table created: {name}")
    /// </summary>
    private QueryResult ExecuteCreateTable(CreateTableStatement stmt)
    {
        _catalog.CreateTable(new Schema(stmt.TableName, stmt.Columns));
        return new QueryResult { Message = $"Table created: {stmt.TableName}" };
    }

    /// <summary>
    /// Executes INSERT INTO.
    ///
    /// Steps:
    ///   1. Get the table's schema and data file path from the catalog
    ///   2. Build the row values array:
    ///      - The INSERT statement has Columns (the column names) and Values (the literal values)
    ///      - Order the values to match the schema's column order
    ///        (i.e., for each column in schema.Columns, find the matching column in stmt.Columns
    ///         and take the corresponding value from stmt.Values)
    ///   3. Serialize the row using RowSerializer.Serialize(schema, orderedValues)
    ///   4. Open a PageManager on the data file, create a HeapFile, insert the serialized row
    ///   5. Return a QueryResult with AffectedRows = 1
    ///
    /// Note: Remember to dispose the PageManager after use.
    /// </summary>
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

    /// <summary>
    /// Executes SELECT.
    ///
    /// Steps:
    ///   1. Get the table's schema and data file path from the catalog
    ///   2. Open a PageManager on the data file, create a HeapFile
    ///   3. Scan all rows from the heap file
    ///   4. For each row:
    ///      a. Deserialize it using RowSerializer.Deserialize(schema, rowBytes)
    ///      b. If there's a WHERE clause, evaluate it using ExpressionEvaluator.Evaluate()
    ///         - If the row doesn't match, skip it
    ///      c. If selecting specific columns (not "*"):
    ///         - Build a new object[] containing only the requested columns' values
    ///      d. Add the (possibly filtered) row to the result
    ///   5. Set the result's Columns:
    ///      - If "*": all column names from the schema
    ///      - Otherwise: the specific column names from the SELECT
    ///   6. Return the QueryResult with Columns and Rows populated
    ///
    /// Note: Remember to dispose the PageManager after use.
    /// </summary>
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

    /// <summary>
    /// Executes DELETE.
    ///
    /// Steps:
    ///   1. Get the table's schema and data file path from the catalog
    ///   2. Open a PageManager on the data file, create a HeapFile
    ///   3. Scan all rows from the heap file
    ///   4. For each row:
    ///      a. Deserialize it
    ///      b. If there's a WHERE clause, evaluate it
    ///         - If the row doesn't match, skip it
    ///      c. If the row matches (or there's no WHERE — delete all), call heapFile.Delete(rowId)
    ///      d. Count the deleted rows
    ///   5. Return a QueryResult with AffectedRows = count of deleted rows
    ///
    /// Note: Remember to dispose the PageManager after use.
    /// </summary>
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

    /// <summary>
    /// Executes CREATE INDEX.
    ///
    /// For now, this is a stub — just return a success message.
    /// The actual B-tree index implementation will come in Lesson 10.
    ///
    /// Return a QueryResult with Message = "Index created: {indexName}"
    /// </summary>
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
