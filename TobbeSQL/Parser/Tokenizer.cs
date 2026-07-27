using System.Text;

namespace TobbeSQL.Parser;

public class Tokenizer
{
    private string _input = "";
    private int _pos;

    public List<Token> Tokenize(string sql)
    {
        _input = sql;
        _pos = 0;
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
                {
                    var next = Peek();
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
                }
                case '>':
                {
                    var next = Peek();
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
                }
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
