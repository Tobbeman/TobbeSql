using TobbeSQL.Parser.Ast;
using TobbeSQL.Storage;

namespace TobbeSQL.Parser;

/// <summary>
/// Recursive descent parser that turns a list of tokens into an AST.
///
/// The parser maintains a position (_pos) into the token list and consumes tokens
/// left-to-right. Each Parse* method handles one grammar rule.
///
/// Grammar (simplified):
///   statement     → createTable | insert | select | delete | createIndex
///   createTable   → CREATE TABLE identifier ( columnDef [, columnDef]* )
///   columnDef     → identifier (INT | VARCHAR)
///   insert        → INSERT INTO identifier ( identifierList ) VALUES ( valueList )
///   select        → SELECT (columnList | *) FROM identifier [WHERE expression]
///   delete        → DELETE FROM identifier [WHERE expression]
///   createIndex   → CREATE INDEX identifier ON identifier ( identifier )
///   expression    → comparison [(AND | OR) comparison]*
///   comparison    → identifier operator value
///   operator      → = | < | > | <= | >= | <>
///   value         → number | stringLiteral
/// </summary>
public class SqlParser
{
    private List<Token> _tokens = new();
    private int _pos;

    /// <summary>
    /// Parses the token list and returns the corresponding AST statement node.
    ///
    /// 1. Set _tokens and _pos = 0.
    /// 2. Look at the first token to decide which statement type to parse:
    ///    - Select → ParseSelect()
    ///    - Insert → ParseInsert()
    ///    - Delete → ParseDelete()
    ///    - Create → peek at next token: Table → ParseCreateTable(), Index → ParseCreateIndex()
    /// 3. Return the resulting statement.
    /// </summary>
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
            _ => throw new Exception($"Could not parse token: {_tokens[_pos].Type}"),
        };
    }

    private Statement ParseCreate()
    {
        var next = Peek();
        return next?.Type switch
        {
            TokenType.Index => ParseCreateIndex(),
            TokenType.Table => ParseCreateTable(),
            _ => throw new Exception($"Cannot parse create since next is: {next?.Type}"),
        };
    }

    /// <summary>
    /// Parses: CREATE TABLE tableName (col1 INT, col2 VARCHAR, ...)
    ///
    /// Assumes CREATE and TABLE have already been consumed (or consume them here).
    /// 1. Consume CREATE, TABLE tokens.
    /// 2. Read the table name (Identifier token).
    /// 3. Consume LeftParen.
    /// 4. Loop: read column name (Identifier) + type (Int or Varchar), add to list.
    ///    If next token is Comma, consume it and continue. If RightParen, break.
    /// 5. Consume RightParen.
    /// 6. Return a CreateTableStatement.
    /// </summary>
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

    /// <summary>
    /// Parses: INSERT INTO tableName (col1, col2) VALUES (val1, val2)
    ///
    /// 1. Consume INSERT, INTO tokens.
    /// 2. Read the table name (Identifier).
    /// 3. Consume LeftParen.
    /// 4. Read column names (Identifiers separated by Commas) until RightParen.
    /// 5. Consume RightParen, then VALUES, then LeftParen.
    /// 6. Read values (Numbers become int, StringLiterals become string) separated by Commas.
    /// 7. Consume RightParen.
    /// 8. Return an InsertStatement.
    /// </summary>
    private InsertStatement ParseInsert()
    {
        Expect(TokenType.Insert);
        Expect(TokenType.Into);
        var tableName = Expect(TokenType.Identifier).Value;
        var columns = new List<string>();
        var values = new List<object>();

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

        Expect(TokenType.LeftParen);
        while (true)
        {
            var columnValueToken = Expect(TokenType.Number, TokenType.StringLiteral);
            values.Add(ParseValue(columnValueToken));

            if (Expect(TokenType.Comma, TokenType.RightParen).Type == TokenType.RightParen)
            {
                break;
            }
        }
        return new InsertStatement(tableName, columns, values);
    }

    /// <summary>
    /// Parses: SELECT (col1, col2 | *) FROM tableName [WHERE expression]
    ///
    /// 1. Consume SELECT.
    /// 2. If next token is Star, add "*" to column list and consume it.
    ///    Otherwise, read column names (Identifiers separated by Commas) until FROM is seen.
    /// 3. Consume FROM.
    /// 4. Read the table name (Identifier).
    /// 5. If there are more tokens and the next is WHERE, consume it and call ParseExpression().
    /// 6. Return a SelectStatement.
    /// </summary>
    private SelectStatement ParseSelect()
    {
        Expect(TokenType.Select);

        var columns = new List<string>();
        Expression? expression = null;

        if (Current().Type == TokenType.Star)
        {
            columns.Add("*");
            Advance();
            Expect(TokenType.From);
        }
        else
        {
            while (true)
            {
                var columnName = Expect(TokenType.Identifier).Value;
                columns.Add(columnName);
                if (Expect(TokenType.Comma, TokenType.From).Type == TokenType.From)
                {
                    break;
                }
            }
        }

        var tableName = Expect(TokenType.Identifier).Value;
        if (HasMore() && Current().Type == TokenType.Where)
        {
            Advance();
            expression = ParseExpression();
        }
        return new SelectStatement(columns, tableName, expression);
    }

    /// <summary>
    /// Parses: DELETE FROM tableName [WHERE expression]
    ///
    /// 1. Consume DELETE, FROM.
    /// 2. Read the table name (Identifier).
    /// 3. If there are more tokens and the next is WHERE, consume it and call ParseExpression().
    /// 4. Return a DeleteStatement.
    /// </summary>
    private DeleteStatement ParseDelete()
    {
        Expect(TokenType.Delete);
        Expect(TokenType.From);
        var tableName = Expect(TokenType.Identifier).Value;
        Expression? expression = null;
        if (HasMore())
        {
            Expect(TokenType.Where);
            expression = ParseExpression();
        }
        return new DeleteStatement(tableName, expression);
    }

    /// <summary>
    /// Parses: CREATE INDEX indexName ON tableName (columnName)
    ///
    /// 1. Consume CREATE, INDEX.
    /// 2. Read the index name (Identifier).
    /// 3. Consume ON.
    /// 4. Read the table name (Identifier).
    /// 5. Consume LeftParen.
    /// 6. Read the column name (Identifier).
    /// 7. Consume RightParen.
    /// 8. Return a CreateIndexStatement.
    /// </summary>
    private CreateIndexStatement ParseCreateIndex()
    {
        Expect(TokenType.Create);
        Expect(TokenType.Index);
        var indexName = Expect(TokenType.Identifier).Value;
        Expect(TokenType.On);
        var tableName = Expect(TokenType.Identifier).Value;
        Expect(TokenType.LeftParen);
        var columnName = Expect(TokenType.Identifier).Value;
        Expect(TokenType.RightParen);
        return new CreateIndexStatement(indexName, tableName, columnName);
    }

    /// <summary>
    /// Parses a WHERE expression: comparison [(AND | OR) comparison]*
    ///
    /// 1. Call ParseComparison() to get the first expression.
    /// 2. While the next token is AND or OR:
    ///    a. Save the operator token.
    ///    b. Consume it.
    ///    c. Call ParseComparison() to get the right side.
    ///    d. Wrap them in a LogicalExpression(left, operator, right).
    ///    e. The result becomes the new "left" for the next iteration.
    /// 3. Return the expression.
    /// </summary>
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

    /// <summary>
    /// Parses a single comparison: columnName operator value
    ///
    /// 1. Read the column name (Identifier token).
    /// 2. Read the operator (Equals, LessThan, GreaterThan, LessThanOrEqual, GreaterThanOrEqual, NotEqual).
    /// 3. Read the value:
    ///    - Number token → parse it as int (int.Parse(token.Value))
    ///    - StringLiteral token → use the string value directly
    /// 4. Return a ComparisonExpression.
    /// </summary>
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

    // --- Helper methods ---

    /// <summary>
    /// Returns the current token without advancing.
    /// </summary>
    private Token Current() => _tokens[_pos];

    /// <summary>
    /// Returns the current token and advances _pos by 1.
    /// </summary>
    private Token Advance() => _tokens[_pos++];

    private Token? Peek() => _pos + 1 < _tokens.Count ? _tokens[_pos + 1] : null;

    /// <summary>
    /// Returns true if there are more tokens to consume.
    /// </summary>
    private bool HasMore() => _pos < _tokens.Count;

    /// <summary>
    /// Consumes the current token, asserting it has the expected type.
    /// Throws if the token type doesn't match.
    /// </summary>
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
}
