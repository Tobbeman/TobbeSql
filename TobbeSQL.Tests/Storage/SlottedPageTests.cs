using TobbeSQL.Storage;

namespace TobbeSQL.Tests.Storage;

public class SlottedPageTests
{
    private byte[] CreateInitializedPage()
    {
        var page = new byte[PageManager.PageSize];
        SlottedPage.Initialize(page);
        return page;
    }

    [Fact]
    public void InsertAndGetRow_ReturnsMatchingData()
    {
        var page = new SlottedPage(CreateInitializedPage());
        var rowData = new byte[] { 1, 2, 3, 4, 5 };

        int slot = page.InsertRow(rowData);

        Assert.Equal(0, slot);
        Assert.Equal(rowData, page.GetRow(slot));
    }

    [Fact]
    public void InsertMultipleRows_EachGetsUniqueSlot()
    {
        var page = new SlottedPage(CreateInitializedPage());
        var row0 = new byte[] { 10, 20 };
        var row1 = new byte[] { 30, 40, 50 };
        var row2 = new byte[] { 60 };

        Assert.Equal(0, page.InsertRow(row0));
        Assert.Equal(1, page.InsertRow(row1));
        Assert.Equal(2, page.InsertRow(row2));

        Assert.Equal(row0, page.GetRow(0));
        Assert.Equal(row1, page.GetRow(1));
        Assert.Equal(row2, page.GetRow(2));
    }

    [Fact]
    public void DeleteRow_GetRowReturnsNull()
    {
        var page = new SlottedPage(CreateInitializedPage());
        int slot = page.InsertRow(new byte[] { 1, 2, 3 });

        page.DeleteRow(slot);

        Assert.Null(page.GetRow(slot));
    }

    [Fact]
    public void InsertUntilFull_ReturnsNegativeOne()
    {
        var page = new SlottedPage(CreateInitializedPage());
        var largeRow = new byte[200];

        int insertCount = 0;
        while (true)
        {
            int slot = page.InsertRow(largeRow);
            if (slot == -1)
                break;
            insertCount++;
        }

        Assert.True(insertCount > 0, "Should have inserted at least one row before filling up");
        Assert.Equal(-1, page.InsertRow(largeRow));
    }

    [Fact]
    public void FreeSpace_DecreasesAfterInsert()
    {
        var page = new SlottedPage(CreateInitializedPage());
        int spaceBefore = page.FreeSpace;

        page.InsertRow(new byte[] { 1, 2, 3, 4, 5 });

        Assert.True(page.FreeSpace < spaceBefore);
    }

    [Fact]
    public void SlotCount_IncrementsOnInsert()
    {
        var page = new SlottedPage(CreateInitializedPage());
        Assert.Equal(0, page.SlotCount);

        page.InsertRow(new byte[] { 1 });
        Assert.Equal(1, page.SlotCount);

        page.InsertRow(new byte[] { 2 });
        Assert.Equal(2, page.SlotCount);
    }
}
