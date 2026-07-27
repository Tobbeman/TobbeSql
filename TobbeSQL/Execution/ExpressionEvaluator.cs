using TobbeSQL.Parser;
using TobbeSQL.Parser.Ast;
using TobbeSQL.Storage;

namespace TobbeSQL.Execution;

public static class ExpressionEvaluator
{
    public static bool Evaluate(Expression expression, Schema schema, object[] rowValues)
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
                var rowValue = rowValues[columnIndex];

                return expr.Operator switch
                {
                    TokenType.Equals => rowValue.Equals(expr.Value),
                    TokenType.NotEqual => !rowValue.Equals(expr.Value),
                    TokenType.LessThan => ((IComparable)rowValue).CompareTo(expr.Value) < 0,
                    TokenType.GreaterThan => ((IComparable)rowValue).CompareTo(expr.Value) > 0,
                    TokenType.LessThanOrEqual => ((IComparable)rowValue).CompareTo(expr.Value) <= 0,
                    TokenType.GreaterThanOrEqual => ((IComparable)rowValue).CompareTo(expr.Value)
                        >= 0,
                    _ => throw new Exception(
                        $"Could not match expression operator: {expr.Operator}"
                    ),
                };
            case LogicalExpression expr:
                return expr.Operator switch
                {
                    TokenType.And => Evaluate(expr.Left, schema, rowValues)
                        && Evaluate(expr.Right, schema, rowValues),
                    TokenType.Or => Evaluate(expr.Left, schema, rowValues)
                        || Evaluate(expr.Right, schema, rowValues),
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
        if (expression is ComparisonExpression { Operator: TokenType.Equals } comp
            && comp.ColumnName == columnName)
        {
            return comp.Value;
        }
        return null;
    }
}
