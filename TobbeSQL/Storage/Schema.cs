namespace TobbeSQL.Storage;

/// <summary>
/// Describes a table: its name and the ordered list of columns.
/// </summary>
public class Schema
{
    public string TableName { get; }
    public List<ColumnDefinition> Columns { get; }

    public Schema(string tableName, List<ColumnDefinition> columns)
    {
        TableName = tableName;
        Columns = columns;
    }
}
