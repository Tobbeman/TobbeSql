namespace TobbeSQL.Parser.Ast;

/// <summary>
/// Represents: CREATE INDEX indexName ON tableName (columnName)
/// </summary>
public class CreateIndexStatement : Statement
{
    public string IndexName { get; }
    public string TableName { get; }
    public string ColumnName { get; }

    public CreateIndexStatement(string indexName, string tableName, string columnName)
    {
        IndexName = indexName;
        TableName = tableName;
        ColumnName = columnName;
    }
}
