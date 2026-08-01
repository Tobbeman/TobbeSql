using TobbeSQL.Parser.Ast;
using TobbeSQL.Storage;

namespace TobbeSQL.Parser;

public class SqlParser
{
    private List<Token> _tokens = new();
    private int _pos;

    public Statement Parse(List<Token> tokens)
    {
        _tokens = tokens;
        _pos = 0;

        return Current().Type switch
        {
            TokenType.Select => ParseSelect(),
            TokenType.Insert => ParseInsert(),
            TokenType.Delete => ParseDelete(),
            TokenType.Create => ParseCreate(),
            TokenType.Drop => ParseDrop(),
            _ => throw new Exception($"Could not parse token: {_tokens[_pos].Type}"),
        };
    }

    private Statement ParseCreate()
    {
        var next = Peek();
        return next?.Type switch
        {
            TokenType.Index or TokenType.Unique => ParseCreateIndex(),
            TokenType.Table => ParseCreateTable(),
            _ => throw new Exception($"Cannot parse create since next is: {next?.Type}"),
        };
    }

    private Statement ParseDrop()
    {
        var next = Peek();
        return next?.Type switch
        {
            TokenType.Index => ParseDropIndex(),
            TokenType.Table => ParseDropTable(),
            _ => throw new Exception($"Cannot parse create since next is: {next?.Type}"),
        };
    }

    private DropIndexStatement ParseDropIndex()
    {
        Expect(TokenType.Drop);
        Expect(TokenType.Index);
        var indexName = Expect(TokenType.Identifier).Value;
        return new DropIndexStatement(indexName);
    }

    private DropTableStatement ParseDropTable()
    {
        Expect(TokenType.Drop);
        Expect(TokenType.Table);
        var tableName = Expect(TokenType.Identifier).Value;
        return new DropTableStatement(tableName);
    }

    private CreateTableStatement ParseCreateTable()
    {
        Expect(TokenType.Create);
        Expect(TokenType.Table);
        var tableName = Expect(TokenType.Identifier).Value;
        var columns = new List<ColumnDefinition>();

        Expect(TokenType.LeftParen);
        while (true)
        {
            var columnNameToken = Expect(TokenType.Identifier);
            var columnTypeToken = Expect(TokenType.Int, TokenType.Varchar);
            var columnType = columnTypeToken.Type switch
            {
                TokenType.Int => ColumnType.Integer,
                TokenType.Varchar => ColumnType.Varchar,
                _ => throw new Exception(
                    $"Could not parse column type from token: {columnTypeToken.Type}"
                ),
            };

            columns.Add(new ColumnDefinition(columnNameToken.Value, columnType));

            if (Expect(TokenType.Comma, TokenType.RightParen).Type == TokenType.RightParen)
            {
                break;
            }
        }

        return new CreateTableStatement(tableName, columns);
    }

    private InsertStatement ParseInsert()
    {
        Expect(TokenType.Insert);
        Expect(TokenType.Into);
        var tableName = Expect(TokenType.Identifier).Value;
        var columns = new List<string>();
        var values = new List<List<object>>();

        Expect(TokenType.LeftParen);
        while (true)
        {
            var columnName = Expect(TokenType.Identifier).Value;
            columns.Add(columnName);
            if (Expect(TokenType.Comma, TokenType.RightParen).Type == TokenType.RightParen)
            {
                break;
            }
        }

        Expect(TokenType.Values);
        do
        {
            var currentValues = new List<object>();
            Expect(TokenType.LeftParen);
            while (true)
            {
                var columnValueToken = Expect(TokenType.Number, TokenType.StringLiteral);
                currentValues.Add(ParseValue(columnValueToken));

                if (Expect(TokenType.Comma, TokenType.RightParen).Type == TokenType.RightParen)
                {
                    break;
                }
            }
            values.Add(currentValues);
        } while (HasMore() && Current().Type == TokenType.Comma && Advance() is not null);

        return new InsertStatement(tableName, columns, values);
    }

    private Statement ParseSelect()
    {
        Expect(TokenType.Select);

        var columns = new List<ColumnExpression>();

        var parse = true;
        while (parse)
        {
            var token = Advance();
            switch (token.Type)
            {
                case TokenType.Star:
                case TokenType.Identifier:
                    columns.Add(new(ColumnExpressionType.Column, token.Value));
                    break;
                case TokenType.Count:
                    Expect(TokenType.LeftParen);
                    var column = Expect(TokenType.Identifier, TokenType.Star);
                    Expect(TokenType.RightParen);
                    columns.Add(new(ColumnExpressionType.Count, column.Value));
                    break;
                case TokenType.Comma:
                    break;
                case TokenType.From:
                    parse = false;
                    break;
                default:
                    throw new Exception($"Could not parse selected column: {token.Type}");
            }
        }

        var tableName = Expect(TokenType.Identifier).Value;
        return new SelectStatement(
            columns,
            tableName,
            GetOptionalWhereExpression(),
            GetOptionalGroupByExpression(),
            GetOptionalLimitExpression()
        );
    }

    private DeleteStatement ParseDelete()
    {
        Expect(TokenType.Delete);
        Expect(TokenType.From);
        var tableName = Expect(TokenType.Identifier).Value;
        return new DeleteStatement(tableName, GetOptionalWhereExpression());
    }

    private CreateIndexStatement ParseCreateIndex()
    {
        Expect(TokenType.Create);
        var unique = Expect(TokenType.Unique, TokenType.Index).Type == TokenType.Unique;
        if (unique)
        {
            Expect(TokenType.Index);
        }
        var indexName = Expect(TokenType.Identifier).Value;
        Expect(TokenType.On);
        var tableName = Expect(TokenType.Identifier).Value;
        Expect(TokenType.LeftParen);
        var columnName = Expect(TokenType.Identifier).Value;
        Expect(TokenType.RightParen);
        return new CreateIndexStatement(indexName, tableName, columnName, unique);
    }

    private Expression ParseExpression()
    {
        var left = (Expression)ParseComparison();
        while (HasMore())
        {
            var token = Advance();
            if (token.Type != TokenType.And && token.Type != TokenType.Or)
            {
                break;
            }

            var rightSide = ParseComparison();
            left = new LogicalExpression(left, token.Type, rightSide);
        }

        return left;
    }

    private ComparisonExpression ParseComparison()
    {
        var columnName = Expect(TokenType.Identifier).Value;
        var op = Expect(
            TokenType.Equals,
            TokenType.LessThan,
            TokenType.GreaterThan,
            TokenType.LessThanOrEqual,
            TokenType.GreaterThanOrEqual,
            TokenType.NotEqual
        );
        var valueType = Expect(TokenType.Number, TokenType.StringLiteral);

        return new ComparisonExpression(columnName, op.Type, ParseValue(valueType));
    }

    private Token Current() => _tokens[_pos];

    private Token Advance() => _tokens[_pos++];

    private Token? Peek() => _pos + 1 < _tokens.Count ? _tokens[_pos + 1] : null;

    private bool HasMore() => _pos < _tokens.Count;

    private Token Expect(params TokenType[] types)
    {
        var token = Advance();
        if (!types.Contains(token.Type))
            throw new Exception(
                $"Expected {string.Join(",", types)} but got {token.Type} ('{token.Value}') at position {_pos - 1}"
            );
        return token;
    }

    private static object ParseValue(Token token)
    {
        return token.Type switch
        {
            TokenType.Number => int.Parse(token.Value),
            TokenType.StringLiteral => token.Value,
            _ => throw new Exception($"Could not parse value token: {token.Type}"),
        };
    }

    private Expression? GetOptionalWhereExpression()
    {
        if (HasMore() && Current().Type == TokenType.Where)
        {
            Advance();
            return ParseExpression();
        }
        return null;
    }

    private List<string>? GetOptionalGroupByExpression()
    {
        if (!HasMore() || Current().Type != TokenType.Group)
        {
            return null;
        }
        var groupBy = new List<string>();

        Advance();
        Expect(TokenType.By);
        while (true)
        {
            groupBy.Add(Expect(TokenType.Identifier).Value);
            if (!HasMore() || Current().Type != TokenType.Comma)
            {
                break;
            }
            Advance();
        }

        return groupBy;
    }

    private int? GetOptionalLimitExpression()
    {
        if (HasMore() && Current().Type == TokenType.Limit)
        {
            Advance();
            var value = Expect(TokenType.Number).Value;
            if (!int.TryParse(value, out var intValue))
            {
                throw new Exception($"Failed to parse int for limit: {value}");
            }
            return intValue;
        }
        return null;
    }
}
