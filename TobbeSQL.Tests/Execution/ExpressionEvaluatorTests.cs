using TobbeSQL.Execution;
using TobbeSQL.Parser;
using TobbeSQL.Parser.Ast;
using TobbeSQL.Storage;

namespace TobbeSQL.Tests.Execution;

public class ExpressionEvaluatorTests
{
    private static Schema MakeSchema(params (string name, ColumnType type)[] columns)
    {
        return new Schema(
            "test",
            columns.Select(c => new ColumnDefinition(c.name, c.type)).ToList()
        );
    }

    private static readonly Schema UsersSchema = MakeSchema(
        ("id", ColumnType.Integer),
        ("name", ColumnType.Varchar),
        ("age", ColumnType.Integer)
    );

    [Fact]
    public void Compile_Equals_MatchesCorrectRow()
    {
        var expr = new ComparisonExpression("id", TokenType.Equals, 2);
        var predicate = ExpressionEvaluator.Compile(expr, UsersSchema);

        Assert.True(predicate(new object[] { 2, "Alice", 30 }));
        Assert.False(predicate(new object[] { 1, "Bob", 25 }));
    }

    [Fact]
    public void Compile_NotEqual_ExcludesMatchingRow()
    {
        var expr = new ComparisonExpression("name", TokenType.NotEqual, "Alice");
        var predicate = ExpressionEvaluator.Compile(expr, UsersSchema);

        Assert.True(predicate(new object[] { 1, "Bob", 25 }));
        Assert.False(predicate(new object[] { 2, "Alice", 30 }));
    }

    [Fact]
    public void Compile_LessThan_ComparesCorrectly()
    {
        var expr = new ComparisonExpression("age", TokenType.LessThan, 30);
        var predicate = ExpressionEvaluator.Compile(expr, UsersSchema);

        Assert.True(predicate(new object[] { 1, "Alice", 25 }));
        Assert.False(predicate(new object[] { 2, "Bob", 30 }));
        Assert.False(predicate(new object[] { 3, "Charlie", 35 }));
    }

    [Fact]
    public void Compile_GreaterThan_ComparesCorrectly()
    {
        var expr = new ComparisonExpression("age", TokenType.GreaterThan, 30);
        var predicate = ExpressionEvaluator.Compile(expr, UsersSchema);

        Assert.False(predicate(new object[] { 1, "Alice", 25 }));
        Assert.False(predicate(new object[] { 2, "Bob", 30 }));
        Assert.True(predicate(new object[] { 3, "Charlie", 35 }));
    }

    [Fact]
    public void Compile_LessThanOrEqual_IncludesBoundary()
    {
        var expr = new ComparisonExpression("age", TokenType.LessThanOrEqual, 30);
        var predicate = ExpressionEvaluator.Compile(expr, UsersSchema);

        Assert.True(predicate(new object[] { 1, "Alice", 25 }));
        Assert.True(predicate(new object[] { 2, "Bob", 30 }));
        Assert.False(predicate(new object[] { 3, "Charlie", 35 }));
    }

    [Fact]
    public void Compile_GreaterThanOrEqual_IncludesBoundary()
    {
        var expr = new ComparisonExpression("age", TokenType.GreaterThanOrEqual, 30);
        var predicate = ExpressionEvaluator.Compile(expr, UsersSchema);

        Assert.False(predicate(new object[] { 1, "Alice", 25 }));
        Assert.True(predicate(new object[] { 2, "Bob", 30 }));
        Assert.True(predicate(new object[] { 3, "Charlie", 35 }));
    }

    [Fact]
    public void Compile_And_RequiresBothConditions()
    {
        var expr = new LogicalExpression(
            new ComparisonExpression("age", TokenType.GreaterThan, 20),
            TokenType.And,
            new ComparisonExpression("name", TokenType.Equals, "Alice")
        );
        var predicate = ExpressionEvaluator.Compile(expr, UsersSchema);

        Assert.True(predicate(new object[] { 1, "Alice", 30 }));
        Assert.False(predicate(new object[] { 2, "Alice", 15 }));
        Assert.False(predicate(new object[] { 3, "Bob", 30 }));
    }

    [Fact]
    public void Compile_Or_RequiresEitherCondition()
    {
        var expr = new LogicalExpression(
            new ComparisonExpression("id", TokenType.Equals, 1),
            TokenType.Or,
            new ComparisonExpression("id", TokenType.Equals, 3)
        );
        var predicate = ExpressionEvaluator.Compile(expr, UsersSchema);

        Assert.True(predicate(new object[] { 1, "Alice", 30 }));
        Assert.False(predicate(new object[] { 2, "Bob", 25 }));
        Assert.True(predicate(new object[] { 3, "Charlie", 35 }));
    }

    [Fact]
    public void Compile_NestedLogical_EvaluatesCorrectly()
    {
        // (id = 1 AND age > 20) OR name = 'Charlie'
        var expr = new LogicalExpression(
            new LogicalExpression(
                new ComparisonExpression("id", TokenType.Equals, 1),
                TokenType.And,
                new ComparisonExpression("age", TokenType.GreaterThan, 20)
            ),
            TokenType.Or,
            new ComparisonExpression("name", TokenType.Equals, "Charlie")
        );
        var predicate = ExpressionEvaluator.Compile(expr, UsersSchema);

        Assert.True(predicate(new object[] { 1, "Alice", 30 }));
        Assert.False(predicate(new object[] { 1, "Alice", 15 }));
        Assert.False(predicate(new object[] { 2, "Bob", 25 }));
        Assert.True(predicate(new object[] { 3, "Charlie", 10 }));
    }

    [Fact]
    public void Compile_UnknownColumn_Throws()
    {
        var expr = new ComparisonExpression("nonexistent", TokenType.Equals, 1);

        Assert.Throws<Exception>(() => ExpressionEvaluator.Compile(expr, UsersSchema));
    }

    [Fact]
    public void Compile_StringComparison_WorksWithLessThan()
    {
        var expr = new ComparisonExpression("name", TokenType.LessThan, "Bob");
        var predicate = ExpressionEvaluator.Compile(expr, UsersSchema);

        Assert.True(predicate(new object[] { 1, "Alice", 30 }));
        Assert.False(predicate(new object[] { 2, "Bob", 25 }));
        Assert.False(predicate(new object[] { 3, "Charlie", 35 }));
    }
}
