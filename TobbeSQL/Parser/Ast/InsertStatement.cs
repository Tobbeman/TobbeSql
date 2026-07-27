namespace TobbeSQL.Parser.Ast;

public record InsertStatement(string TableName, List<string> Columns, List<List<object>> Values)
    : Statement;
