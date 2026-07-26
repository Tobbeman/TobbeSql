using TobbeSQL.Execution;
using TobbeSQL.Parser;
using TobbeSQL.Parser.Ast;
using TobbeSQL.Storage;

namespace TobbeSQL.Tests.Execution;

public class QueryExecutorTests : IDisposable
{
    private readonly string _testDir;
    private readonly Catalog _catalog;
    private readonly QueryExecutor _executor;
    private readonly Tokenizer _tokenizer;
    private readonly SqlParser _parser;

    public QueryExecutorTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"tobbesql_test_{Guid.NewGuid()}");
        _catalog = new Catalog(_testDir);
        _executor = new QueryExecutor(_catalog);
        _tokenizer = new Tokenizer();
        _parser = new SqlParser();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, recursive: true);
    }

    /// <summary>
    /// Helper: parses a SQL string and executes it, returning the result.
    /// Use this in your tests to avoid repeating tokenize+parse+execute boilerplate.
    /// </summary>
    private QueryResult Run(string sql)
    {
        var tokens = _tokenizer.Tokenize(sql);
        var statement = _parser.Parse(tokens);
        return _executor.Execute(statement);
    }

    [Fact]
    public void CreateTable_ThenSelectStar_ReturnsEmptyResult()
    {
        Run("CREATE TABLE users (id INT, name VARCHAR)");
        var result = Run("SELECT * FROM users");

        Assert.Empty(result.Rows);
        Assert.Equal(2, result.Columns.Count);
        Assert.Contains("id", result.Columns);
        Assert.Contains("name", result.Columns);
    }

    [Fact]
    public void InsertAndSelect_ReturnsInsertedRow()
    {
        Run("CREATE TABLE users (id INT, name VARCHAR)");
        Run("INSERT INTO users (id, name) VALUES (1, 'Alice')");
        var result = Run("SELECT * FROM users");

        Assert.Single(result.Rows);
        Assert.Equal(1, result.Rows[0][0]);
        Assert.Equal("Alice", result.Rows[0][1]);
    }

    [Fact]
    public void InsertMultiple_SelectAll_ReturnsAllRows()
    {
        Run("CREATE TABLE users (id INT, name VARCHAR)");
        Run("INSERT INTO users (id, name) VALUES (1, 'Alice')");
        Run("INSERT INTO users (id, name) VALUES (2, 'Bob')");
        Run("INSERT INTO users (id, name) VALUES (3, 'Charlie')");
        var result = Run("SELECT * FROM users");

        Assert.Equal(3, result.Rows.Count);
    }

    [Fact]
    public void SelectWithWhere_ReturnsOnlyMatchingRows()
    {
        Run("CREATE TABLE users (id INT, name VARCHAR)");
        Run("INSERT INTO users (id, name) VALUES (1, 'Alice')");
        Run("INSERT INTO users (id, name) VALUES (2, 'Bob')");
        Run("INSERT INTO users (id, name) VALUES (3, 'Charlie')");
        var result = Run("SELECT * FROM users WHERE id = 2");

        Assert.Single(result.Rows);
        Assert.Equal("Bob", result.Rows[0][1]);
    }

    [Fact]
    public void Delete_RemovesMatchingRows()
    {
        Run("CREATE TABLE users (id INT, name VARCHAR)");
        Run("INSERT INTO users (id, name) VALUES (1, 'Alice')");
        Run("INSERT INTO users (id, name) VALUES (2, 'Bob')");
        var deleteResult = Run("DELETE FROM users WHERE id = 1");

        Assert.Equal(1, deleteResult.AffectedRows);

        var selectResult = Run("SELECT * FROM users");
        Assert.Single(selectResult.Rows);
        Assert.Equal("Bob", selectResult.Rows[0][1]);
    }

    [Fact]
    public void SelectSpecificColumns_ReturnsOnlyThoseColumns()
    {
        Run("CREATE TABLE users (id INT, name VARCHAR)");
        Run("INSERT INTO users (id, name) VALUES (1, 'Alice')");
        var result = Run("SELECT name FROM users");

        Assert.Single(result.Columns);
        Assert.Equal("name", result.Columns[0]);
        Assert.Single(result.Rows);
        Assert.Single(result.Rows[0]);
        Assert.Equal("Alice", result.Rows[0][0]);
    }
}
