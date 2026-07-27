namespace TobbeSQL.Storage;

public class HeapFile
{
    private readonly PageManager _pageManager;

    public HeapFile(PageManager pageManager)
    {
        _pageManager = pageManager;
    }

    public RowId Insert(byte[] rowData)
    {
        int pageNumber;
        SlottedPage page;
        int slotNumber;
        for (pageNumber = 0; pageNumber < _pageManager.PageCount; pageNumber++)
        {
            page = new SlottedPage(_pageManager.ReadPage(pageNumber));
            slotNumber = page.InsertRow(rowData);
            if (slotNumber != -1)
            {
                _pageManager.WritePage(pageNumber, page.GetPageData());
                return new RowId(pageNumber, slotNumber);
            }
        }

        pageNumber = _pageManager.AllocatePage();
        var rawPage = _pageManager.ReadPage(pageNumber);
        SlottedPage.Initialize(rawPage);
        page = new SlottedPage(rawPage);
        slotNumber = page.InsertRow(rowData);
        _pageManager.WritePage(pageNumber, page.GetPageData());
        return new RowId(pageNumber, slotNumber);
    }

    public byte[]? GetRow(RowId rowId)
    {
        var page = new SlottedPage(_pageManager.ReadPage(rowId.PageNumber));
        return page.GetRow(rowId.SlotNumber);
    }

    public void Delete(RowId rowId)
    {
        var page = new SlottedPage(_pageManager.ReadPage(rowId.PageNumber));
        page.DeleteRow(rowId.SlotNumber);
        _pageManager.WritePage(rowId.PageNumber, page.GetPageData());
    }

    public IEnumerable<(RowId rowId, byte[] data)> Scan()
    {
        for (var pageNumber = 0; pageNumber < _pageManager.PageCount; pageNumber++)
        {
            var page = new SlottedPage(_pageManager.ReadPage(pageNumber));
            for (var slotNumber = 0; slotNumber < page.SlotCount; slotNumber++)
            {
                var slot = page.GetRow(slotNumber);
                if (slot == null)
                {
                    continue;
                }

                yield return (new RowId(pageNumber, slotNumber), slot);
            }
        }
    }
}
