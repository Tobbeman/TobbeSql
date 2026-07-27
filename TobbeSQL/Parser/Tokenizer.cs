using System.Text;

namespace TobbeSQL.Parser;

/// <summary>
/// Breaks a SQL string into a list of tokens.
///
/// The tokenizer reads the input character by character using a position index (_pos).
/// It skips whitespace, then determines what kind of token starts at the current position:
///
/// - If the character is a digit → read all consecutive digits as a Number token
/// - If the character is a single quote → read until the closing quote as a StringLiteral
///   (the value should NOT include the quotes themselves)
/// - If the character is a letter or underscore → read all consecutive letters/digits/underscores,
///   then check if the word is a keyword (case-insensitive). If yes, produce the keyword token.
///   If no, produce an Identifier token.
/// - If the character is an operator or punctuation → produce the appropriate token.
///   For two-character operators (<>, <=, >=), peek at the next character before deciding.
///
/// Keywords to recognize (case-insensitive):
///   SELECT, FROM, WHERE, INSERT, INTO, VALUES, DELETE, CREATE, TABLE, INDEX, ON, AND, OR, NOT, INT, VARCHAR
/// </summary>
public class Tokenizer
{
    private string _input = "";
    private int _pos;

    /// <summary>
    /// Tokenizes the given SQL string into a list of tokens.
    ///
    /// Algorithm:
    /// 1. Set _input and _pos = 0.
    /// 2. Loop while _pos < _input.Length:
    ///    a. Skip any whitespace characters.
    ///    b. If at end, break.
    ///    c. Look at the current character and decide what to read:
    ///       - '(' → add LeftParen token, advance _pos
    ///       - ')' → add RightParen token, advance _pos
    ///       - ',' → add Comma token, advance _pos
    ///       - '*' → add Star token, advance _pos
    ///       - '=' → add Equals token, advance _pos
    ///       - '<' → peek next: if '=' → LessThanOrEqual (advance 2),
    ///               if '>' → NotEqual (advance 2), else LessThan (advance 1)
    ///       - '>' → peek next: if '=' → GreaterThanOrEqual (advance 2),
    ///               else GreaterThan (advance 1)
    ///       - '\'' (single quote) → call ReadString()
    ///       - digit → call ReadNumber()
    ///       - letter or '_' → call ReadWord()
    ///       - anything else → throw an exception (unexpected character)
    /// 3. Return the token list.
    /// </summary>
    public List<Token> Tokenize(string sql)
    {
        _input = sql;
        _pos = 0;
        char? next;
        var tokens = new List<Token>();
        while (_pos < _input.Length)
        {
            var character = _input[_pos];

            switch (character)
            {
                case ' ':
                    _pos++;
                    break;
                case '(':
                    tokens.Add(new Token(TokenType.LeftParen, "("));
                    _pos++;
                    break;
                case ')':
                    tokens.Add(new Token(TokenType.RightParen, ")"));
                    _pos++;
                    break;
                case ',':
                    tokens.Add(new Token(TokenType.Comma, ","));
                    _pos++;
                    break;
                case '*':
                    tokens.Add(new Token(TokenType.Star, "*"));
                    _pos++;
                    break;
                case '=':
                    tokens.Add(new Token(TokenType.Equals, "="));
                    _pos++;
                    break;
                case '<':
                    next = Peek();
                    switch (next)
                    {
                        case '=':
                            tokens.Add(new Token(TokenType.LessThanOrEqual, "<="));
                            _pos++;
                            break;
                        case '>':
                            tokens.Add(new Token(TokenType.NotEqual, "<>"));
                            _pos++;
                            break;
                        default:
                            tokens.Add(new Token(TokenType.LessThan, "<"));
                            break;
                    }
                    _pos++;
                    break;
                case '>':
                    next = Peek();
                    switch (next)
                    {
                        case '=':
                            tokens.Add(new Token(TokenType.GreaterThanOrEqual, ">="));
                            _pos++;
                            break;
                        default:
                            tokens.Add(new Token(TokenType.GreaterThan, ">"));
                            break;
                    }
                    _pos++;
                    break;
                case '\'':
                    tokens.Add(ReadString());
                    break;
                default:
                    if (char.IsLetter(character) || character == '_')
                    {
                        tokens.Add(ReadWord());
                    }
                    else if (char.IsDigit(character))
                    {
                        tokens.Add(ReadNumber());
                    }
                    else
                    {
                        throw new Exception($"Could not tokenize character: {character},{_pos}");
                    }
                    break;
            }
        }

        return tokens;
    }

    /// <summary>
    /// Reads a number token starting at the current position.
    /// Consume all consecutive digit characters, return them as a Number token.
    /// </summary>
    private Token ReadNumber()
    {
        var sb = new StringBuilder();
        while (_pos < _input.Length && char.IsDigit(_input[_pos]))
        {
            sb.Append(_input[_pos]);
            _pos++;
        }

        return new Token(TokenType.Number, sb.ToString());
    }

    /// <summary>
    /// Reads a string literal (text between single quotes).
    /// _pos starts ON the opening quote.
    /// Advance past the opening quote, then read characters until the closing quote.
    /// The token value is the text between the quotes (not including the quotes).
    /// Advance _pos past the closing quote.
    /// </summary>
    private Token ReadString()
    {
        var sb = new StringBuilder();
        _pos++;
        while (_pos < _input.Length && _input[_pos] != '\'')
        {
            sb.Append(_input[_pos]);
            _pos++;
        }
        _pos++;

        return new Token(TokenType.StringLiteral, sb.ToString());
    }

    /// <summary>
    /// Reads a word (identifier or keyword) starting at the current position.
    /// Consume all consecutive letters, digits, and underscores.
    /// Then check if the word (case-insensitive) matches a known keyword.
    /// If it does, return the appropriate keyword token.
    /// If not, return an Identifier token.
    ///
    /// Use a dictionary or switch to map keyword strings to TokenTypes:
    ///   "select" → Select, "from" → From, "where" → Where, etc.
    /// </summary>
    private Token ReadWord()
    {
        var sb = new StringBuilder();
        while (_pos < _input.Length && (char.IsLetterOrDigit(_input[_pos]) || _input[_pos] == '_'))
        {
            sb.Append(_input[_pos]);
            _pos++;
        }

        var word = sb.ToString();
        var type = word.ToLower() switch
        {
            "select" => TokenType.Select,
            "from" => TokenType.From,
            "where" => TokenType.Where,
            "insert" => TokenType.Insert,
            "into" => TokenType.Into,
            "values" => TokenType.Values,
            "delete" => TokenType.Delete,
            "create" => TokenType.Create,
            "table" => TokenType.Table,
            "index" => TokenType.Index,
            "on" => TokenType.On,
            "and" => TokenType.And,
            "or" => TokenType.Or,
            "int" => TokenType.Int,
            "varchar" => TokenType.Varchar,
            _ => TokenType.Identifier,
        };

        return new Token(type, word);
    }

    private char? Peek()
    {
        if (_pos + 1 < _input.Length)
        {
            return _input[_pos + 1];
        }
        return null;
    }
}
