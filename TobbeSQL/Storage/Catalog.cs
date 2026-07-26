using System.Text.Json;

namespace TobbeSQL.Storage;

/// <summary>
/// Stores metadata about all tables in the database.
/// Persists to a JSON file so table definitions survive restarts.
///
/// The catalog tracks for each table:
/// - The schema (table name, columns with names and types)
/// - The data file path (where the table's heap file is stored)
///
/// The catalog file and all data files live in the same directory.
/// </summary>
public class Catalog
{
    private readonly string _directoryPath;
    private readonly string _catalogFilePath;

    // Maps table name → (Schema, data file path)
    private Dictionary<string, (Schema schema, string dataFilePath)> _tables = new();

    /// <summary>
    /// Creates a Catalog that stores its metadata in the given directory.
    /// If the directory doesn't exist, create it.
    /// If a catalog.json file already exists there, load it (call Load()).
    /// </summary>
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

    /// <summary>
    /// Registers a new table in the catalog.
    ///
    /// 1. Check if a table with this name already exists — if so, throw an exception.
    /// 2. Choose a data file path for the table: Path.Combine(_directoryPath, $"{schema.TableName}.db")
    /// 3. Add the entry to the _tables dictionary.
    /// 4. Create the data file on disk (just an empty file — the HeapFile will use it via PageManager).
    /// 5. Call Save() to persist the catalog.
    /// </summary>
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

    /// <summary>
    /// Returns the schema and data file path for the given table name.
    /// Throws if the table doesn't exist.
    /// </summary>
    public (Schema schema, string dataFilePath) GetTable(string tableName)
    {
        if (!_tables.TryGetValue(tableName, out var table))
        {
            throw new Exception($"Table does not exist: {tableName}");
        }
        return table;
    }

    /// <summary>
    /// Returns true if a table with the given name exists in the catalog.
    /// </summary>
    public bool TableExists(string tableName)
    {
        return _tables.ContainsKey(tableName);
    }

    /// <summary>
    /// Returns all table names in the catalog.
    /// </summary>
    public IEnumerable<string> TableNames => _tables.Keys;

    /// <summary>
    /// Saves the catalog to catalog.json in the directory.
    ///
    /// You'll need a serializable representation since Schema/ColumnDefinition aren't
    /// directly JSON-friendly. A simple approach:
    /// - Create a helper class/record (e.g. CatalogEntry) with:
    ///   string TableName, string DataFilePath, List of (string Name, string Type) for columns
    /// - Convert _tables to a list of CatalogEntry, then serialize with JsonSerializer.
    /// </summary>
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
                }
            )
        );
    }

    /// <summary>
    /// Loads the catalog from catalog.json in the directory.
    ///
    /// If the file doesn't exist, do nothing (empty catalog).
    /// Otherwise, deserialize the JSON and rebuild the _tables dictionary,
    /// converting the stored column types back into ColumnType enum values.
    /// </summary>
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
    }

    record CatalogFile
    {
        public List<CatalogEntry> Tables { get; set; } = [];
    };

    record CatalogEntry
    {
        public required string TableName { get; set; }
        public required string DataFilePath { get; set; }
        public required List<ColumnDefinition> Columns { get; set; }
    }
}
