namespace TobbeSQL.Parser.Ast;

/// <summary>
/// Represents: SELECT columns FROM tableName [WHERE expression]
/// If Columns contains "*", it means select all columns.
/// </summary>
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
