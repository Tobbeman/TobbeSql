namespace TobbeSQL.Parser.Ast;

/// <summary>
/// Base class for WHERE clause expressions.
/// </summary>
public abstract class Expression { }

/// <summary>
/// A comparison between a column and a value: columnName op value
/// Example: id = 5, name = 'Alice', age > 18
///
/// The Value is either an int or a string (matching our supported types).
/// </summary>
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

/// <summary>
/// A logical AND/OR combining two expressions: left AND right, left OR right
/// </summary>
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
