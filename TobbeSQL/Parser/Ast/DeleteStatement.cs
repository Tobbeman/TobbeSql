namespace TobbeSQL.Parser.Ast;

public class DeleteStatement : Statement
{
    public string TableName { get; }
    public Expression? WhereClause { get; }

    public DeleteStatement(string tableName, Expression? whereClause)
    {
        TableName = tableName;
        WhereClause = whereClause;
    }
}
