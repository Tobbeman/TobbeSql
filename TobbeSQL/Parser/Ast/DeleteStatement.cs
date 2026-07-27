namespace TobbeSQL.Parser.Ast;

public record DeleteStatement(string TableName, Expression? WhereClause) : Statement;
