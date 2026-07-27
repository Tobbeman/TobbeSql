namespace TobbeSQL.Parser.Ast;

public record CountStatement(string TableName, Expression? WhereClause) : Statement;
