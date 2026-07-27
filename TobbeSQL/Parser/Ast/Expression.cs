namespace TobbeSQL.Parser.Ast;

public abstract record Expression;

public record ComparisonExpression(string ColumnName, TokenType Operator, object Value)
    : Expression;

public record LogicalExpression(Expression Left, TokenType Operator, Expression Right)
    : Expression;
