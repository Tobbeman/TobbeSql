namespace TobbeSQL.Storage;

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
