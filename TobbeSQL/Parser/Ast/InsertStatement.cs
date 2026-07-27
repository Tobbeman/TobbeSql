namespace TobbeSQL.Parser.Ast;

public class InsertStatement : Statement
{
    public string TableName { get; }
    public List<string> Columns { get; }
    public List<List<object>> Values { get; }

    public InsertStatement(string tableName, List<string> columns, List<List<object>> values)
    {
        TableName = tableName;
        Columns = columns;
        Values = values;
    }
}
