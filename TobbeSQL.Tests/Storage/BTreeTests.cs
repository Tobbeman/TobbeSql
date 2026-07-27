using TobbeSQL.Storage;

namespace TobbeSQL.Tests.Storage;

public class BTreeTests : IDisposable
{
    private readonly string _tempFile;
    private readonly PageManager _pageManager;

    public BTreeTests()
    {
        _tempFile = Path.GetTempFileName();
        _pageManager = new PageManager(_tempFile);
    }

    public void Dispose()
    {
        _pageManager.Dispose();
        File.Delete(_tempFile);
    }

    [Fact]
    public void InsertAndSearch_SingleKey_ReturnsCorrectRowId()
    {
        var tree = BTree.Create(_pageManager);
        var rowId = new RowId(1, 5);

        tree.Insert(42, rowId);
        var results = tree.Search(42);

        Assert.Single(results);
        Assert.Equal(1, results[0].PageNumber);
        Assert.Equal(5, results[0].SlotNumber);
    }

    [Fact]
    public void InsertMany_SearchEach_AllFound()
    {
        var tree = BTree.Create(_pageManager);

        for (var i = 0; i < 20; i++)
        {
            tree.Insert(i * 10, new RowId(i, i));
        }

        for (var i = 0; i < 20; i++)
        {
            var results = tree.Search(i * 10);
            Assert.Single(results);
            Assert.Equal(i, results[0].PageNumber);
            Assert.Equal(i, results[0].SlotNumber);
        }
    }

    [Fact]
    public void Search_NonExistentKey_ReturnsEmpty()
    {
        var tree = BTree.Create(_pageManager);
        tree.Insert(10, new RowId(0, 0));

        var results = tree.Search(99);

        Assert.Empty(results);
    }

    [Fact]
    public void InsertEnoughToSplit_StillFindsAllKeys()
    {
        var tree = BTree.Create(_pageManager);

        for (var i = 0; i < 500; i++)
        {
            tree.Insert(i, new RowId(i / 100, i % 100));
        }

        for (var i = 0; i < 500; i++)
        {
            var results = tree.Search(i);
            Assert.Single(results);
            Assert.Equal(i / 100, results[0].PageNumber);
            Assert.Equal(i % 100, results[0].SlotNumber);
        }
    }

    [Fact]
    public void Delete_RemovesKeyFromSearch()
    {
        var tree = BTree.Create(_pageManager);
        var rowId = new RowId(3, 7);

        tree.Insert(50, rowId);
        tree.Delete(50, rowId);

        var results = tree.Search(50);
        Assert.Empty(results);
    }

    [Fact]
    public void DuplicateKeys_ReturnsAllMatchingRowIds()
    {
        var tree = BTree.Create(_pageManager);
        tree.Insert(42, new RowId(1, 0));
        tree.Insert(42, new RowId(2, 1));
        tree.Insert(42, new RowId(3, 2));

        var results = tree.Search(42);

        Assert.Equal(3, results.Count);
    }

    [Fact]
    public void InsertReverseOrder_StillFindsAllKeys()
    {
        var tree = BTree.Create(_pageManager);

        for (var i = 299; i >= 0; i--)
        {
            tree.Insert(i, new RowId(i, 0));
        }

        for (var i = 0; i < 300; i++)
        {
            var results = tree.Search(i);
            Assert.Single(results);
            Assert.Equal(i, results[0].PageNumber);
        }
    }

    [Fact(Skip = "Takes a while, only run manually")]
    public void InsertOneMillion_StillFindsAllKeys()
    {
        var tree = BTree.Create(_pageManager);
        var count = 1000000;

        for (var i = 0; i < count; i++)
        {
            tree.Insert(i, new RowId(i / 100, i % 100));
        }

        // Spot-check a sampling of keys
        for (var i = 0; i < count; i += 1000)
        {
            var results = tree.Search(i);
            Assert.Single(results);
            Assert.Equal(i / 100, results[0].PageNumber);
            Assert.Equal(i % 100, results[0].SlotNumber);
        }

        // Check first and last
        var first = tree.Search(0);
        Assert.Single(first);
        var last = tree.Search(count - 1);
        Assert.Single(last);
    }

    [Fact]
    public void DuplicateKeys_CauseExceptionIfNotAllowed()
    {
        var tree = BTree.Create(_pageManager);
        tree.Insert(42, new RowId(1, 0), false);
        Assert.Throws<Exception>(() => tree.Insert(42, new RowId(2, 1), false));
    }
}
