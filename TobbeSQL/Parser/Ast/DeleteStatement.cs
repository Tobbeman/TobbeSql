namespace TobbeSQL.Parser.Ast;

/// <summary>
/// Represents: DELETE FROM tableName [WHERE expression]
/// </summary>
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
