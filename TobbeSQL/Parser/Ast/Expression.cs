namespace TobbeSQL.Parser.Ast;

public abstract class Expression { }

public class ComparisonExpression : Expression
{
    public string ColumnName { get; }
    public TokenType Operator { get; }
    public object Value { get; }

    public ComparisonExpression(string columnName, TokenType op, object value)
    {
        ColumnName = columnName;
        Operator = op;
        Value = value;
    }
}

public class LogicalExpression : Expression
{
    public Expression Left { get; }
    public TokenType Operator { get; }
    public Expression Right { get; }

    public LogicalExpression(Expression left, TokenType op, Expression right)
    {
        Left = left;
        Operator = op;
        Right = right;
    }
}
