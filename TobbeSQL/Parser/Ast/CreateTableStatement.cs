using TobbeSQL.Storage;

namespace TobbeSQL.Parser.Ast;

public record CreateTableStatement(string TableName, List<ColumnDefinition> Columns) : Statement;
