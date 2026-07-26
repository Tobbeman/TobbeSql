using TobbeSQL.Parser;
using TobbeSQL.Parser.Ast;
using TobbeSQL.Storage;

namespace TobbeSQL.Tests.Parser;

public class ParserTests
{
    private readonly Tokenizer _tokenizer = new();
    private readonly SqlParser _parser = new();

    private Statement Parse(string sql) => _parser.Parse(_tokenizer.Tokenize(sql));

    [Fact]
    public void Parse_CreateTable_HasCorrectTableNameAndColumns()
    {
        var stmt = Parse("CREATE TABLE users (id INT, name VARCHAR)") as CreateTableStatement;

        Assert.NotNull(stmt);
        Assert.Equal("users", stmt.TableName);
        Assert.Equal(2, stmt.Columns.Count);
        Assert.Equal("id", stmt.Columns[0].Name);
        Assert.Equal(ColumnType.Integer, stmt.Columns[0].Type);
        Assert.Equal("name", stmt.Columns[1].Name);
        Assert.Equal(ColumnType.Varchar, stmt.Columns[1].Type);
    }

    [Fact]
    public void Parse_Insert_HasTableColumnsAndValues()
    {
        var stmt = Parse("INSERT INTO users (id, name) VALUES (1, 'Alice')") as InsertStatement;

        Assert.NotNull(stmt);
        Assert.Equal("users", stmt.TableName);
        Assert.Equal(new List<string> { "id", "name" }, stmt.Columns);
        Assert.Equal(2, stmt.Values.Count);
        Assert.Equal(1, stmt.Values[0]);
        Assert.Equal("Alice", stmt.Values[1]);
    }

    [Fact]
    public void Parse_SelectStar_HasWildcardAndTableName()
    {
        var stmt = Parse("SELECT * FROM users") as SelectStatement;

        Assert.NotNull(stmt);
        Assert.Equal(new List<string> { "*" }, stmt.Columns);
        Assert.Equal("users", stmt.TableName);
        Assert.Null(stmt.WhereClause);
    }

    [Fact]
    public void Parse_SelectWithWhere_HasExpression()
    {
        var stmt = Parse("SELECT name FROM users WHERE id = 1") as SelectStatement;

        Assert.NotNull(stmt);
        Assert.Equal(new List<string> { "name" }, stmt.Columns);
        Assert.Equal("users", stmt.TableName);

        var where = stmt.WhereClause as ComparisonExpression;
        Assert.NotNull(where);
        Assert.Equal("id", where.ColumnName);
        Assert.Equal(TokenType.Equals, where.Operator);
        Assert.Equal(1, where.Value);
    }

    [Fact]
    public void Parse_Delete_HasTableAndWhereClause()
    {
        var stmt = Parse("DELETE FROM users WHERE id = 5") as DeleteStatement;

        Assert.NotNull(stmt);
        Assert.Equal("users", stmt.TableName);

        var where = stmt.WhereClause as ComparisonExpression;
        Assert.NotNull(where);
        Assert.Equal("id", where.ColumnName);
        Assert.Equal(TokenType.Equals, where.Operator);
        Assert.Equal(5, where.Value);
    }

    [Fact]
    public void Parse_CreateIndex_HasIndexNameTableAndColumn()
    {
        var stmt = Parse("CREATE INDEX idx_id ON users (id)") as CreateIndexStatement;

        Assert.NotNull(stmt);
        Assert.Equal("idx_id", stmt.IndexName);
        Assert.Equal("users", stmt.TableName);
        Assert.Equal("id", stmt.ColumnName);
    }

    [Fact]
    public void Parse_WhereWithAnd_ProducesLogicalExpression()
    {
        var stmt = Parse("SELECT * FROM users WHERE id > 3 AND name = 'Alice'") as SelectStatement;

        Assert.NotNull(stmt);
        var logical = stmt.WhereClause as LogicalExpression;
        Assert.NotNull(logical);
        Assert.Equal(TokenType.And, logical.Operator);

        var left = logical.Left as ComparisonExpression;
        Assert.NotNull(left);
        Assert.Equal("id", left.ColumnName);
        Assert.Equal(TokenType.GreaterThan, left.Operator);
        Assert.Equal(3, left.Value);

        var right = logical.Right as ComparisonExpression;
        Assert.NotNull(right);
        Assert.Equal("name", right.ColumnName);
        Assert.Equal(TokenType.Equals, right.Operator);
        Assert.Equal("Alice", right.Value);
    }
}
