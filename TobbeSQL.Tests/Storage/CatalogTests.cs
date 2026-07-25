using TobbeSQL.Storage;

namespace TobbeSQL.Tests.Storage;

public class CatalogTests : IDisposable
{
    private readonly string _testDirectory;

    public CatalogTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"catalog_test_{Guid.NewGuid()}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, recursive: true);
    }

    private Schema MakeSchema(string tableName, params (string name, ColumnType type)[] columns)
    {
        return new Schema(
            tableName,
            columns.Select(c => new ColumnDefinition(c.name, c.type)).ToList()
        );
    }

    [Fact]
    public void CreateTable_ThenTableExists_ReturnsTrue()
    {
        var catalog = new Catalog(_testDirectory);
        var schema = MakeSchema("users", ("id", ColumnType.Integer), ("name", ColumnType.Varchar));

        catalog.CreateTable(schema);

        Assert.True(catalog.TableExists("users"));
    }

    [Fact]
    public void GetTable_ReturnsCorrectSchema()
    {
        var catalog = new Catalog(_testDirectory);
        var schema = MakeSchema("users", ("id", ColumnType.Integer), ("name", ColumnType.Varchar));
        catalog.CreateTable(schema);

        var (retrievedSchema, dataFilePath) = catalog.GetTable("users");

        Assert.Equal("users", retrievedSchema.TableName);
        Assert.Equal(2, retrievedSchema.Columns.Count);
        Assert.Equal("id", retrievedSchema.Columns[0].Name);
        Assert.Equal(ColumnType.Integer, retrievedSchema.Columns[0].Type);
        Assert.Equal("name", retrievedSchema.Columns[1].Name);
        Assert.Equal(ColumnType.Varchar, retrievedSchema.Columns[1].Type);
        Assert.False(string.IsNullOrEmpty(dataFilePath));
    }

    [Fact]
    public void CreateDuplicateTable_Throws()
    {
        var catalog = new Catalog(_testDirectory);
        var schema = MakeSchema("users", ("id", ColumnType.Integer));
        catalog.CreateTable(schema);

        Assert.Throws<Exception>(() => catalog.CreateTable(schema));
    }

    [Fact]
    public void SaveAndLoad_PreservesTablesAcrossRestarts()
    {
        var schema = MakeSchema(
            "products",
            ("id", ColumnType.Integer),
            ("title", ColumnType.Varchar)
        );

        var catalog1 = new Catalog(_testDirectory);
        catalog1.CreateTable(schema);

        var catalog2 = new Catalog(_testDirectory);

        Assert.True(catalog2.TableExists("products"));
        var (loaded, _) = catalog2.GetTable("products");
        Assert.Equal("products", loaded.TableName);
        Assert.Equal(2, loaded.Columns.Count);
        Assert.Equal("id", loaded.Columns[0].Name);
        Assert.Equal(ColumnType.Integer, loaded.Columns[0].Type);
        Assert.Equal("title", loaded.Columns[1].Name);
        Assert.Equal(ColumnType.Varchar, loaded.Columns[1].Type);
    }

    [Fact]
    public void TableExists_UnknownTable_ReturnsFalse()
    {
        var catalog = new Catalog(_testDirectory);

        Assert.False(catalog.TableExists("nonexistent"));
    }
}
