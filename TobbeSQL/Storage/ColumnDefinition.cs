namespace TobbeSQL.Storage;

public class ColumnDefinition
{
    public string Name { get; }
    public ColumnType Type { get; }

    public ColumnDefinition(string name, ColumnType type)
    {
        Name = name;
        Type = type;
    }
}
