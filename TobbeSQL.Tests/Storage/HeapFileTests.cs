using TobbeSQL.Storage;

namespace TobbeSQL.Tests.Storage;

public class HeapFileTests : IDisposable
{
    private readonly string _testFilePath;
    private readonly PageManager _pageManager;
    private readonly HeapFile _heapFile;

    public HeapFileTests()
    {
        _testFilePath = Path.Combine(Path.GetTempPath(), $"heapfile_test_{Guid.NewGuid()}.db");
        _pageManager = new PageManager(_testFilePath);
        _heapFile = new HeapFile(_pageManager);
    }

    public void Dispose()
    {
        _pageManager.Dispose();
        if (File.Exists(_testFilePath))
            File.Delete(_testFilePath);
    }

    [Fact]
    public void InsertAndGetRow_ReturnsSameData()
    {
        var rowData = new byte[] { 1, 2, 3, 4, 5 };

        var rowId = _heapFile.Insert(rowData);
        var result = _heapFile.GetRow(rowId);

        Assert.Equal(rowData, result);
    }

    [Fact]
    public void InsertManyRows_SpansMultiplePages()
    {
        // Each row is 500 bytes. A 4096-byte page fits ~7-8 rows (accounting for header/slots).
        // Inserting 20 rows should require at least 3 pages.
        var rowData = new byte[500];
        Array.Fill(rowData, (byte)0xAA);

        var rowIds = new List<RowId>();
        for (int i = 0; i < 20; i++)
            rowIds.Add(_heapFile.Insert(rowData));

        // Verify rows ended up on more than one page
        var distinctPages = rowIds.Select(r => r.PageNumber).Distinct().Count();
        Assert.True(distinctPages > 1, $"Expected multiple pages, got {distinctPages}");

        // Verify all rows are readable
        var scanResults = _heapFile.Scan().ToList();
        Assert.Equal(20, scanResults.Count);
    }

    [Fact]
    public void Scan_ReturnsAllInsertedRows()
    {
        var rows = new List<byte[]>
        {
            new byte[] { 10, 20 },
            new byte[] { 30, 40, 50 },
            new byte[] { 60 },
        };

        foreach (var row in rows)
            _heapFile.Insert(row);

        var scanResults = _heapFile.Scan().ToList();

        Assert.Equal(3, scanResults.Count);
        Assert.Contains(scanResults, r => r.data.SequenceEqual(new byte[] { 10, 20 }));
        Assert.Contains(scanResults, r => r.data.SequenceEqual(new byte[] { 30, 40, 50 }));
        Assert.Contains(scanResults, r => r.data.SequenceEqual(new byte[] { 60 }));
    }

    [Fact]
    public void Delete_RemovesRowFromScan()
    {
        var id1 = _heapFile.Insert(new byte[] { 1 });
        var id2 = _heapFile.Insert(new byte[] { 2 });
        var id3 = _heapFile.Insert(new byte[] { 3 });

        _heapFile.Delete(id2);

        var scanResults = _heapFile.Scan().ToList();
        Assert.Equal(2, scanResults.Count);
        Assert.Contains(scanResults, r => r.data.SequenceEqual(new byte[] { 1 }));
        Assert.Contains(scanResults, r => r.data.SequenceEqual(new byte[] { 3 }));
    }

    [Fact]
    public void GetRow_AfterDelete_ReturnsNull()
    {
        var rowId = _heapFile.Insert(new byte[] { 1, 2, 3 });

        _heapFile.Delete(rowId);

        Assert.Null(_heapFile.GetRow(rowId));
    }

    [Fact]
    public void InsertBatchAndGetRows_ReturnsSameData()
    {
        var rowData = new List<byte[]>
        {
            new byte[] { 1, 2, 3, 4, 5 },
            new byte[] { 6, 7, 8, 9, 10 },
        };

        var rowIds = _heapFile.InsertBatch(rowData).ToList();
        for (var i = 0; i < rowIds.Count; i++)
        {
            var result = _heapFile.GetRow(rowIds[i]);
            Assert.Equal(rowData[i], result);
        }

        var scanResults = _heapFile.Scan().ToList();
        Assert.Equal(rowData.Count, scanResults.Count);
    }
}
