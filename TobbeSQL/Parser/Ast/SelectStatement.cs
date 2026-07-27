namespace TobbeSQL.Parser.Ast;

public class SelectStatement : Statement
{
    public List<string> Columns { get; }
    public string TableName { get; }
    public Expression? WhereClause { get; }

    public SelectStatement(List<string> columns, string tableName, Expression? whereClause)
    {
        Columns = columns;
        TableName = tableName;
        WhereClause = whereClause;
    }
}
