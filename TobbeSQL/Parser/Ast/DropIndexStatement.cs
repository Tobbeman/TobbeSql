namespace TobbeSQL.Parser.Ast;

public record DropIndexStatement(string IndexName) : Statement;
