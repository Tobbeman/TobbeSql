using System.Text.Json;

namespace TobbeSQL.Storage;

public class Catalog
{
    private readonly string _directoryPath;
    private readonly string _catalogFilePath;

    private Dictionary<string, (Schema schema, string dataFilePath)> _tables = new();
    private List<(
        string IndexName,
        string TableName,
        string ColumnName,
        string DataFilePath,
        bool Unique
    )> _indexes = new();

    public Catalog(string directoryPath)
    {
        _directoryPath = directoryPath;
        _catalogFilePath = Path.Combine(directoryPath, "catalog.json");

        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        if (!File.Exists(_catalogFilePath))
        {
            return;
        }

        Load();
    }

    public string CreateIndex(string indexName, string tableName, string columnName, bool unique)
    {
        if (_indexes.Any(i => i.IndexName == indexName))
        {
            throw new Exception($"Index already exists: {indexName}");
        }
        var dataFile = Path.Combine(_directoryPath, $"idx_{indexName}.db");
        File.Create(dataFile).Dispose();
        _indexes.Add(new(indexName, tableName, columnName, dataFile, unique));
        Save();
        return dataFile;
    }

    public (string DataFilePath, bool Unique)? GetIndex(string tableName, string columnName)
    {
        var match = _indexes.FirstOrDefault(i =>
            i.TableName == tableName && i.ColumnName == columnName
        );

        if (match == default)
        {
            return null;
        }

        return (match.DataFilePath, match.Unique);
    }

    public void CreateTable(Schema schema)
    {
        if (_tables.ContainsKey(schema.TableName))
        {
            throw new Exception($"Table already exists: {schema.TableName}");
        }

        var dataFile = Path.Combine(_directoryPath, $"{schema.TableName}.db");
        _tables.Add(schema.TableName, (schema, dataFile));
        File.Create(dataFile).Dispose();
        Save();
    }

    public (Schema schema, string dataFilePath) GetTable(string tableName)
    {
        if (!_tables.TryGetValue(tableName, out var table))
        {
            throw new Exception($"Table does not exist: {tableName}");
        }
        return table;
    }

    public bool TableExists(string tableName)
    {
        return _tables.ContainsKey(tableName);
    }

    public IEnumerable<string> TableNames => _tables.Keys;

    public void Save()
    {
        File.WriteAllText(
            _catalogFilePath,
            JsonSerializer.Serialize(
                new CatalogFile
                {
                    Tables =
                    [
                        .. _tables.Select(t => new CatalogEntry
                        {
                            TableName = t.Value.schema.TableName,
                            DataFilePath = t.Value.dataFilePath,
                            Columns = t.Value.schema.Columns,
                        }),
                    ],
                    Indexes =
                    [
                        .. _indexes.Select(i => new IndexEntry
                        {
                            Name = i.IndexName,
                            TableName = i.TableName,
                            ColumnName = i.ColumnName,
                            DataFilePath = i.DataFilePath,
                            Unique = i.Unique,
                        }),
                    ],
                }
            )
        );
    }

    public void Load()
    {
        if (!File.Exists(_catalogFilePath))
        {
            return;
        }

        var catalog = JsonSerializer.Deserialize<CatalogFile>(File.ReadAllBytes(_catalogFilePath));
        if (catalog is null)
        {
            return;
        }

        _tables = catalog.Tables.ToDictionary(
            t => t.TableName,
            t => (new Schema(t.TableName, t.Columns), t.DataFilePath)
        );
        _indexes =
        [
            .. catalog.Indexes.Select(i =>
                (i.Name, i.TableName, i.ColumnName, i.DataFilePath, i.Unique)
            ),
        ];
    }

    public void DropIndex(string indexName)
    {
        var index = _indexes.SingleOrDefault(i => i.IndexName == indexName);
        if (index == default)
        {
            throw new Exception($"No such index to drop: {indexName}");
        }

        _indexes.Remove(index);
        File.Delete(index.DataFilePath);
        Save();
    }

    public void DropTable(string tableName)
    {
        if (!_tables.TryGetValue(tableName, out var table))
        {
            throw new Exception($"No such table to drop: {tableName}");
        }

        _tables.Remove(tableName);
        File.Delete(table.dataFilePath);
        Save();
    }

    record CatalogFile
    {
        public required List<CatalogEntry> Tables { get; set; } = [];
        public required List<IndexEntry> Indexes { get; set; } = [];
    };

    record CatalogEntry
    {
        public required string TableName { get; set; }
        public required string DataFilePath { get; set; }
        public required List<ColumnDefinition> Columns { get; set; }
    }

    record IndexEntry
    {
        public required string Name { get; set; }
        public required string TableName { get; set; }
        public required string ColumnName { get; set; }
        public required string DataFilePath { get; set; }
        public required bool Unique { get; set; }
    }
}
