using TobbeSQL.Storage;

namespace TobbeSQL.Tests.Storage;

public class PageManagerTests : IDisposable
{
    private readonly string _testFilePath;

    public PageManagerTests()
    {
        _testFilePath = Path.Combine(Path.GetTempPath(), $"pagemanager_test_{Guid.NewGuid()}.db");
    }

    public void Dispose()
    {
        if (File.Exists(_testFilePath))
            File.Delete(_testFilePath);
    }

    [Fact]
    public void NewFile_HasZeroPages()
    {
        using var pm = new PageManager(_testFilePath);
        Assert.Equal(0, pm.PageCount);
    }

    [Fact]
    public void AllocatePage_ReturnsIncrementingPageNumbers()
    {
        using var pm = new PageManager(_testFilePath);

        Assert.Equal(0, pm.AllocatePage());
        Assert.Equal(1, pm.AllocatePage());
        Assert.Equal(2, pm.AllocatePage());
        Assert.Equal(3, pm.PageCount);
    }

    [Fact]
    public void WriteThenRead_ReturnsSameData()
    {
        using var pm = new PageManager(_testFilePath);
        int pageNum = pm.AllocatePage();

        var data = new byte[PageManager.PageSize];
        for (int i = 0; i < data.Length; i++)
            data[i] = 0xAB;

        pm.WritePage(pageNum, data);
        var readBack = pm.ReadPage(pageNum);

        Assert.Equal(data, readBack);
    }

    [Fact]
    public void DataSurvivesCloseAndReopen()
    {
        var data = new byte[PageManager.PageSize];
        for (int i = 0; i < data.Length; i++)
            data[i] = 0xCD;

        using (var pm = new PageManager(_testFilePath))
        {
            int pageNum = pm.AllocatePage();
            pm.WritePage(pageNum, data);
        }

        using (var pm = new PageManager(_testFilePath))
        {
            Assert.Equal(1, pm.PageCount);
            var readBack = pm.ReadPage(0);
            Assert.Equal(data, readBack);
        }
    }

    [Fact]
    public void ReadPage_UnwrittenPage_ReturnsZeroes()
    {
        using var pm = new PageManager(_testFilePath);
        pm.AllocatePage();

        var readBack = pm.ReadPage(0);

        Assert.Equal(new byte[PageManager.PageSize], readBack);
    }
}
