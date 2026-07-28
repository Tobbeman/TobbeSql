namespace TobbeSQL.Parser.Ast;

public record CreateIndexStatement(
    string IndexName,
    string TableName,
    string ColumnName,
    bool Unique
) : Statement;
