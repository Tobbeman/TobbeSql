namespace TobbeSQL.Storage;

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
