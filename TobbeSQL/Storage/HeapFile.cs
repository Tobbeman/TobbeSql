namespace TobbeSQL.Storage;

/// <summary>
/// A heap file stores rows for a single table across one or more pages.
/// It uses a PageManager for page I/O and SlottedPage for within-page row management.
/// </summary>
public class HeapFile
{
    private readonly PageManager _pageManager;

    /// <summary>
    /// Creates a HeapFile backed by the given PageManager.
    /// The PageManager's file may already contain pages (existing table) or be empty (new table).
    /// </summary>
    public HeapFile(PageManager pageManager)
    {
        _pageManager = pageManager;
    }

    /// <summary>
    /// Inserts a row into the heap file and returns its RowId.
    ///
    /// 1. Iterate through existing pages (0 to PageCount - 1).
    /// 2. For each page, read it, wrap it in a SlottedPage, try to insert.
    /// 3. If the insert succeeds (slot != -1), write the page back to disk and return the RowId.
    /// 4. If no existing page has space, allocate a new page, initialize it (SlottedPage.Initialize),
    ///    insert the row, write the page, and return the RowId.
    /// </summary>
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

    /// <summary>
    /// Retrieves a row by its RowId.
    ///
    /// 1. Read the page at rowId.PageNumber.
    /// 2. Wrap it in a SlottedPage.
    /// 3. Call GetRow(rowId.SlotNumber) and return the result (may be null if deleted).
    /// </summary>
    public byte[]? GetRow(RowId rowId)
    {
        var page = new SlottedPage(_pageManager.ReadPage(rowId.PageNumber));
        return page.GetRow(rowId.SlotNumber);
    }

    /// <summary>
    /// Deletes a row by its RowId.
    ///
    /// 1. Read the page at rowId.PageNumber.
    /// 2. Wrap it in a SlottedPage.
    /// 3. Call DeleteRow(rowId.SlotNumber).
    /// 4. Write the modified page back to disk.
    /// </summary>
    public void Delete(RowId rowId)
    {
        var page = new SlottedPage(_pageManager.ReadPage(rowId.PageNumber));
        page.DeleteRow(rowId.SlotNumber);
        _pageManager.WritePage(rowId.PageNumber, page.GetPageData());
    }

    /// <summary>
    /// Scans all rows in the heap file, yielding (RowId, byte[]) pairs for non-deleted rows.
    ///
    /// 1. Iterate through all pages (0 to PageCount - 1).
    /// 2. For each page, wrap it in a SlottedPage.
    /// 3. Iterate through all slots (0 to SlotCount - 1).
    /// 4. For each slot, call GetRow. If not null, yield the RowId and the row data.
    ///
    /// Use "yield return" to make this an iterator method (IEnumerable).
    /// </summary>
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
