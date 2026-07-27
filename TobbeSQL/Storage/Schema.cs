namespace TobbeSQL.Storage;

public record Schema(string TableName, List<ColumnDefinition> Columns);
