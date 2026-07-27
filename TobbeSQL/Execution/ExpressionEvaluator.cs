using TobbeSQL.Parser;
using TobbeSQL.Parser.Ast;
using TobbeSQL.Storage;

namespace TobbeSQL.Execution;

public static class ExpressionEvaluator
{
    public static Func<object[], bool> Compile(Expression expression, Schema schema)
    {
        var columns = schema.Columns;
        switch (expression)
        {
            case ComparisonExpression expr:
                var columnIndex = columns.FindIndex(c => c.Name == expr.ColumnName);
                if (columnIndex == -1)
                {
                    throw new Exception(
                        $"Could not match expression no such column: {expr.ColumnName}"
                    );
                }

                return expr.Operator switch
                {
                    TokenType.Equals => rowValues => rowValues[columnIndex].Equals(expr.Value),
                    TokenType.NotEqual => rowValues => !rowValues[columnIndex].Equals(expr.Value),
                    TokenType.LessThan => rowValues =>
                        ((IComparable)rowValues[columnIndex]).CompareTo(expr.Value) < 0,
                    TokenType.GreaterThan => rowValues =>
                        ((IComparable)rowValues[columnIndex]).CompareTo(expr.Value) > 0,
                    TokenType.LessThanOrEqual => rowValues =>
                        ((IComparable)rowValues[columnIndex]).CompareTo(expr.Value) <= 0,
                    TokenType.GreaterThanOrEqual => rowValues =>
                        ((IComparable)rowValues[columnIndex]).CompareTo(expr.Value) >= 0,
                    _ => throw new Exception(
                        $"Could not match expression operator: {expr.Operator}"
                    ),
                };
            case LogicalExpression expr:
                var left = Compile(expr.Left, schema);
                var right = Compile(expr.Right, schema);

                return expr.Operator switch
                {
                    TokenType.And => rowValues => left(rowValues) && right(rowValues),
                    TokenType.Or => rowValues => left(rowValues) || right(rowValues),
                    _ => throw new Exception(
                        $"Could not match expression operator: {expr.Operator}"
                    ),
                };

            default:
                throw new Exception($"Could not match expression: {expression.GetType()}");
        }
    }

    public static object? IndexComparison(Expression expression, string columnName)
    {
        if (
            expression is ComparisonExpression { Operator: TokenType.Equals } comp
            && comp.ColumnName == columnName
        )
        {
            return comp.Value;
        }
        return null;
    }
}
