namespace TobbeSQL.Parser.Ast;

public record DropTableStatement(string TableName) : Statement;
