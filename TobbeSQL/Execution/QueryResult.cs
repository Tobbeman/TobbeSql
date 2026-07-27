namespace TobbeSQL.Execution;

public class QueryResult
{
    public List<string> Columns { get; set; } = new();
    public List<object[]> Rows { get; set; } = new();
    public int AffectedRows { get; set; }
    public string? Message { get; set; }
}
