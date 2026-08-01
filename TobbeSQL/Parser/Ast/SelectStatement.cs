namespace TobbeSQL.Parser.Ast;

public record SelectStatement(
    List<ColumnExpression> Columns,
    string TableName,
    Expression? WhereClause,
    List<string>? GroupByColumns,
    int? Limit
) : Statement;

public record ColumnExpression(ColumnExpressionType Type, string Identifier);

public enum ColumnExpressionType
{
    Column,
    Count,
    Min,
    Max,
}
