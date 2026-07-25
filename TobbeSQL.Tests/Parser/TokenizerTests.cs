using TobbeSQL.Parser;

namespace TobbeSQL.Tests.Parser;

public class TokenizerTests
{
    private readonly Tokenizer _tokenizer = new();

    [Fact]
    public void Tokenize_Select_ProducesCorrectTokens()
    {
        var tokens = _tokenizer.Tokenize("SELECT name FROM users WHERE id = 5");

        Assert.Equal(TokenType.Select, tokens[0].Type);
        Assert.Equal(TokenType.Identifier, tokens[1].Type);
        Assert.Equal("name", tokens[1].Value);
        Assert.Equal(TokenType.From, tokens[2].Type);
        Assert.Equal(TokenType.Identifier, tokens[3].Type);
        Assert.Equal("users", tokens[3].Value);
        Assert.Equal(TokenType.Where, tokens[4].Type);
        Assert.Equal(TokenType.Identifier, tokens[5].Type);
        Assert.Equal("id", tokens[5].Value);
        Assert.Equal(TokenType.Equals, tokens[6].Type);
        Assert.Equal(TokenType.Number, tokens[7].Type);
        Assert.Equal("5", tokens[7].Value);
        Assert.Equal(8, tokens.Count);
    }

    [Fact]
    public void Tokenize_Insert_ProducesCorrectTokens()
    {
        var tokens = _tokenizer.Tokenize("INSERT INTO users (id, name) VALUES (1, 'Alice')");

        Assert.Equal(TokenType.Insert, tokens[0].Type);
        Assert.Equal(TokenType.Into, tokens[1].Type);
        Assert.Equal(TokenType.Identifier, tokens[2].Type);
        Assert.Equal("users", tokens[2].Value);
        Assert.Equal(TokenType.LeftParen, tokens[3].Type);
        Assert.Equal(TokenType.Identifier, tokens[4].Type);
        Assert.Equal("id", tokens[4].Value);
        Assert.Equal(TokenType.Comma, tokens[5].Type);
        Assert.Equal(TokenType.Identifier, tokens[6].Type);
        Assert.Equal("name", tokens[6].Value);
        Assert.Equal(TokenType.RightParen, tokens[7].Type);
        Assert.Equal(TokenType.Values, tokens[8].Type);
        Assert.Equal(TokenType.LeftParen, tokens[9].Type);
        Assert.Equal(TokenType.Number, tokens[10].Type);
        Assert.Equal("1", tokens[10].Value);
        Assert.Equal(TokenType.Comma, tokens[11].Type);
        Assert.Equal(TokenType.StringLiteral, tokens[12].Type);
        Assert.Equal("Alice", tokens[12].Value);
        Assert.Equal(TokenType.RightParen, tokens[13].Type);
        Assert.Equal(14, tokens.Count);
    }

    [Fact]
    public void Tokenize_StringLiteral_PreservesValue()
    {
        var tokens = _tokenizer.Tokenize("'hello world'");

        Assert.Single(tokens);
        Assert.Equal(TokenType.StringLiteral, tokens[0].Type);
        Assert.Equal("hello world", tokens[0].Value);
    }

    [Fact]
    public void Tokenize_Operators_RecognizedCorrectly()
    {
        var tokens = _tokenizer.Tokenize("<= >= <>");

        Assert.Equal(3, tokens.Count);
        Assert.Equal(TokenType.LessThanOrEqual, tokens[0].Type);
        Assert.Equal(TokenType.GreaterThanOrEqual, tokens[1].Type);
        Assert.Equal(TokenType.NotEqual, tokens[2].Type);
    }

    [Fact]
    public void Tokenize_KeywordsAreCaseInsensitive()
    {
        var tokens1 = _tokenizer.Tokenize("select");
        var tokens2 = _tokenizer.Tokenize("SELECT");
        var tokens3 = _tokenizer.Tokenize("Select");

        Assert.Equal(TokenType.Select, tokens1[0].Type);
        Assert.Equal(TokenType.Select, tokens2[0].Type);
        Assert.Equal(TokenType.Select, tokens3[0].Type);
    }

    [Fact]
    public void Tokenize_CreateTable_ProducesCorrectTokens()
    {
        var tokens = _tokenizer.Tokenize("CREATE TABLE users (id INT, name VARCHAR)");

        Assert.Equal(TokenType.Create, tokens[0].Type);
        Assert.Equal(TokenType.Table, tokens[1].Type);
        Assert.Equal(TokenType.Identifier, tokens[2].Type);
        Assert.Equal("users", tokens[2].Value);
        Assert.Equal(TokenType.LeftParen, tokens[3].Type);
        Assert.Equal(TokenType.Identifier, tokens[4].Type);
        Assert.Equal(TokenType.Int, tokens[5].Type);
        Assert.Equal(TokenType.Comma, tokens[6].Type);
        Assert.Equal(TokenType.Identifier, tokens[7].Type);
        Assert.Equal(TokenType.Varchar, tokens[8].Type);
        Assert.Equal(TokenType.RightParen, tokens[9].Type);
        Assert.Equal(10, tokens.Count);
    }

    [Fact]
    public void Tokenize_Delete_ProducesCorrectTokens()
    {
        var tokens = _tokenizer.Tokenize("DELETE FROM users WHERE id = 5");

        Assert.Equal(TokenType.Delete, tokens[0].Type);
        Assert.Equal(TokenType.From, tokens[1].Type);
        Assert.Equal(TokenType.Identifier, tokens[2].Type);
        Assert.Equal(TokenType.Where, tokens[3].Type);
        Assert.Equal(TokenType.Identifier, tokens[4].Type);
        Assert.Equal(TokenType.Equals, tokens[5].Type);
        Assert.Equal(TokenType.Number, tokens[6].Type);
        Assert.Equal(7, tokens.Count);
    }

    [Fact]
    public void Tokenize_CreateIndex_ProducesCorrectTokens()
    {
        var tokens = _tokenizer.Tokenize("CREATE INDEX idx_id ON users (id)");

        Assert.Equal(TokenType.Create, tokens[0].Type);
        Assert.Equal(TokenType.Index, tokens[1].Type);
        Assert.Equal(TokenType.Identifier, tokens[2].Type);
        Assert.Equal("idx_id", tokens[2].Value);
        Assert.Equal(TokenType.On, tokens[3].Type);
        Assert.Equal(TokenType.Identifier, tokens[4].Type);
        Assert.Equal("users", tokens[4].Value);
        Assert.Equal(TokenType.LeftParen, tokens[5].Type);
        Assert.Equal(TokenType.Identifier, tokens[6].Type);
        Assert.Equal(TokenType.RightParen, tokens[7].Type);
        Assert.Equal(8, tokens.Count);
    }

    [Fact]
    public void Tokenize_WhereWithAndOr_ProducesCorrectTokens()
    {
        var tokens = _tokenizer.Tokenize("SELECT * FROM users WHERE id = 1 AND name = 'Bob' OR id = 2");

        Assert.Equal(TokenType.Select, tokens[0].Type);
        Assert.Equal(TokenType.Star, tokens[1].Type);
        Assert.Equal(TokenType.From, tokens[2].Type);
        Assert.Equal(TokenType.Identifier, tokens[3].Type);
        Assert.Equal(TokenType.Where, tokens[4].Type);
        Assert.Equal(TokenType.Identifier, tokens[5].Type);
        Assert.Equal(TokenType.Equals, tokens[6].Type);
        Assert.Equal(TokenType.Number, tokens[7].Type);
        Assert.Equal(TokenType.And, tokens[8].Type);
        Assert.Equal(TokenType.Identifier, tokens[9].Type);
        Assert.Equal(TokenType.Equals, tokens[10].Type);
        Assert.Equal(TokenType.StringLiteral, tokens[11].Type);
        Assert.Equal(TokenType.Or, tokens[12].Type);
        Assert.Equal(TokenType.Identifier, tokens[13].Type);
        Assert.Equal(TokenType.Equals, tokens[14].Type);
        Assert.Equal(TokenType.Number, tokens[15].Type);
        Assert.Equal(16, tokens.Count);
    }
}
