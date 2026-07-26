using TobbeSQL.Storage;

namespace TobbeSQL.Parser.Ast;

/// <summary>
/// Represents: CREATE TABLE tableName (col1 INT, col2 VARCHAR, ...)
/// </summary>
public class CreateTableStatement : Statement
{
    public string TableName { get; }
    public List<ColumnDefinition> Columns { get; }

    public CreateTableStatement(string tableName, List<ColumnDefinition> columns)
    {
        TableName = tableName;
        Columns = columns;
    }
}
