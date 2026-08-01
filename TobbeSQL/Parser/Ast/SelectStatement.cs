namespace TobbeSQL.Parser.Ast;

public record SelectStatement(
    List<string> Columns,
    string TableName,
    Expression? WhereClause,
    int? Limit
) : Statement;
