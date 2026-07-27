using TobbeSQL.Parser;
using TobbeSQL.Parser.Ast;
using TobbeSQL.Storage;

namespace TobbeSQL.Execution;

/// <summary>
/// Evaluates a WHERE clause expression against a deserialized row.
///
/// Usage: call Evaluate(expression, schema, rowValues) to determine if a row matches.
/// </summary>
public static class ExpressionEvaluator
{
    /// <summary>
    /// Evaluates whether the given row satisfies the expression.
    ///
    /// Parameters:
    ///   - expression: the WHERE clause AST node (ComparisonExpression or LogicalExpression)
    ///   - schema: the table schema (needed to find which column index a column name refers to)
    ///   - rowValues: the deserialized row values (object[]), in the same order as schema.Columns
    ///
    /// Returns: true if the row matches the expression, false otherwise.
    ///
    /// Implementation:
    ///   1. If expression is a ComparisonExpression:
    ///      a. Find the column index by matching expression.ColumnName against schema.Columns
    ///      b. Get the row's value at that index
    ///      c. Compare rowValue against expression.Value using expression.Operator:
    ///         - Equals: rowValue.Equals(expression.Value)
    ///         - NotEqual: !rowValue.Equals(expression.Value)
    ///         - For LessThan, GreaterThan, LessThanOrEqual, GreaterThanOrEqual:
    ///           cast both values to IComparable and use CompareTo()
    ///      d. Return the comparison result
    ///
    ///   2. If expression is a LogicalExpression:
    ///      a. Recursively evaluate Left and Right
    ///      b. If operator is And: return left && right
    ///      c. If operator is Or: return left || right
    ///
    ///   3. Otherwise, throw an exception for unsupported expression types.
    /// </summary>
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
