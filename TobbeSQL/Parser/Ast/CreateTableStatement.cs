using TobbeSQL.Storage;

namespace TobbeSQL.Parser.Ast;

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
