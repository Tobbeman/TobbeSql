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
        return InsertBatch(new List<byte[]>() { rowData }).Single();
    }

    public IEnumerable<RowId> InsertBatch(IEnumerable<byte[]> rowData)
    {
        int pageNumber = 0;
        var rowDataEnumerator = rowData.GetEnumerator();
        if (!rowDataEnumerator.MoveNext())
        {
            throw new Exception("Cannot batch insert, no items");
        }

        do
        {
            SlottedPage page;
            var slotNumber = -1;
            for (; pageNumber < _pageManager.PageCount; pageNumber++)
            {
                page = new SlottedPage(_pageManager.ReadPage(pageNumber));
                slotNumber = page.InsertRow(rowDataEnumerator.Current);
                if (slotNumber != -1)
                {
                    _pageManager.WritePage(pageNumber, page.GetPageData());
                    yield return new RowId(pageNumber, slotNumber);
                    break;
                }
            }

            if (slotNumber != -1)
            {
                continue;
            }

            pageNumber = _pageManager.AllocatePage();
            var rawPage = _pageManager.ReadPage(pageNumber);
            SlottedPage.Initialize(rawPage);
            page = new SlottedPage(rawPage);
            slotNumber = page.InsertRow(rowDataEnumerator.Current);
            _pageManager.WritePage(pageNumber, page.GetPageData());
            yield return new RowId(pageNumber, slotNumber);
        } while (rowDataEnumerator.MoveNext());
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
