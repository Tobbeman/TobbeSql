namespace TobbeSQL.Parser;

public enum TokenType
{
    // Keywords
    Select,
    From,
    Where,
    Insert,
    Into,
    Values,
    Delete,
    Create,
    Count,
    Table,
    Unique,
    Index,
    Drop,
    On,
    And,
    Or,
    Int,
    Varchar,

    // Literals
    Number,
    StringLiteral,

    // Identifiers (table names, column names)
    Identifier,

    // Operators and punctuation
    Equals,
    LessThan,
    GreaterThan,
    LessThanOrEqual,
    GreaterThanOrEqual,
    NotEqual,
    LeftParen,
    RightParen,
    Comma,
    Star,
}
