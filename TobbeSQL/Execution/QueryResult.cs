namespace TobbeSQL.Execution;

/// <summary>
/// Holds the result of executing a SQL statement.
///
/// For SELECT:
///   - Columns contains the column names in the result set
///   - Rows contains the data: each element is an object[] with values in column order
///   - AffectedRows is 0
///
/// For INSERT/DELETE:
///   - Columns is empty
///   - Rows is empty
///   - AffectedRows is the number of rows inserted or deleted
///
/// For CREATE TABLE / CREATE INDEX:
///   - Columns is empty
///   - Rows is empty
///   - AffectedRows is 0
///   - Message contains a success description (e.g. "Table created: users")
/// </summary>
public class QueryResult
{
    public List<string> Columns { get; set; } = new();
    public List<object[]> Rows { get; set; } = new();
    public int AffectedRows { get; set; }
    public string? Message { get; set; }
}
