namespace TobbeSQL.Storage;

/// <summary>
/// Defines a single column in a table: its name and data type.
/// </summary>
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
