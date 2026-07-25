namespace TobbeSQL.Storage;

/// <summary>
/// Uniquely identifies a row within a heap file.
/// A RowId is the combination of which page the row is on and which slot within that page.
/// </summary>
public readonly struct RowId
{
    public int PageNumber { get; }
    public int SlotNumber { get; }

    public RowId(int pageNumber, int slotNumber)
    {
        PageNumber = pageNumber;
        SlotNumber = slotNumber;
    }
}
