namespace TobbeSQL.Parser;

/// <summary>
/// Represents a single token from the SQL input.
/// </summary>
public class Token
{
    public TokenType Type { get; }
    public string Value { get; }

    public Token(TokenType type, string value)
    {
        Type = type;
        Value = value;
    }
}
