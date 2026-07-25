namespace TobbeSQL.Storage;

/// <summary>
/// Provides structure within a raw 4096-byte page to hold multiple variable-length rows.
///
/// Page layout:
///   [Header (4 bytes)] [Slot array, growing forward] [... free space ...] [Row data, growing backward]
///
/// Header:
///   Bytes 0-1: slot count (UInt16)
///   Bytes 2-3: free space offset (UInt16) — points to the byte where the next row data would end
///              (i.e., the lowest occupied byte in the row data area)
///              Initialized to PageManager.PageSize (4096), meaning all space after the header is free.
///
/// Slot array (starts at byte 4, each slot is 4 bytes):
///   Bytes 0-1: row offset (UInt16) — where the row starts in the page. 0 means deleted.
///   Bytes 2-3: row length (UInt16)
///
/// Row data is packed from the END of the page, growing backward toward the header/slots.
///
/// To insert a row:
///   1. Check there's enough free space for the row data + a new slot (4 bytes).
///      Free space = freeSpaceOffset - (HeaderSize + slotCount * SlotSize)
///   2. Calculate the new row's offset: freeSpaceOffset - rowData.Length
///   3. Copy the row data into the page at that offset.
///   4. Write a new slot entry (at position HeaderSize + slotCount * SlotSize) with the offset and length.
///   5. Increment the slot count and update the free space offset in the header.
///
/// To delete a row:
///   Set the slot's row offset to 0 (marks it as deleted).
///   Don't reclaim space — this keeps things simple.
/// </summary>
public class SlottedPage
{
    private const int HeaderSize = 4;
    private const int SlotSize = 4;

    private readonly byte[] _data;

    /// <summary>
    /// Wraps an existing page byte array.
    /// The array should already be initialized (via Initialize) or read from disk.
    /// </summary>
    public SlottedPage(byte[] data)
    {
        _data = data;
    }

    /// <summary>
    /// Initializes a fresh page: sets slot count to 0 and free space offset to PageSize (4096).
    /// Call this once on a new byte[PageSize] before using it as a slotted page.
    /// </summary>
    public static void Initialize(byte[] page)
    {
        WriteToArray(BitConverter.GetBytes((short)0), page, 0);
        WriteToArray(BitConverter.GetBytes((short)PageManager.PageSize), page, 2);
    }

    /// <summary>
    /// Returns the number of slots (including deleted ones).
    /// Read from the header: bytes 0-1 as UInt16.
    /// </summary>
    public int SlotCount
    {
        get { return BitConverter.ToUInt16(_data, 0); }
    }

    /// <summary>
    /// Returns how many bytes are available for new rows + slots.
    /// Free space = freeSpaceOffset - (HeaderSize + SlotCount * SlotSize)
    /// </summary>
    public int FreeSpace
    {
        get { return BitConverter.ToUInt16(_data, 2) - (HeaderSize + SlotCount * SlotSize); }
    }

    /// <summary>
    /// Inserts a row into the page.
    ///
    /// 1. Check if there's enough free space for rowData.Length + SlotSize (4 bytes for the new slot).
    ///    If not, return -1.
    /// 2. Calculate the new row offset: freeSpaceOffset - rowData.Length.
    /// 3. Copy rowData into _data at that offset (use Array.Copy or Buffer.BlockCopy).
    /// 4. Write the new slot at position HeaderSize + slotCount * SlotSize:
    ///    - 2 bytes: row offset (UInt16)
    ///    - 2 bytes: row length (UInt16)
    /// 5. Update the header: increment slot count, set new free space offset.
    /// 6. Return the slot number (which is the old slot count, i.e., 0-based index).
    /// </summary>
    public int InsertRow(byte[] rowData)
    {
        if (FreeSpace < rowData.Length + SlotSize)
        {
            return -1;
        }

        var slotCount = SlotCount;

        // TL: Data
        var freeSpaceOffset = BitConverter.ToUInt16(_data, 2);
        var offset = freeSpaceOffset - rowData.Length;
        WriteToArray(rowData, _data, offset);

        // TL: Slot
        var slotPosition = HeaderSize + slotCount * SlotSize;
        WriteToArray(BitConverter.GetBytes((short)offset), _data, slotPosition);
        WriteToArray(BitConverter.GetBytes((short)rowData.Length), _data, slotPosition + 2);

        // TL: Header
        WriteToArray(BitConverter.GetBytes((short)(slotCount + 1)), _data, 0);
        WriteToArray(BitConverter.GetBytes((short)offset), _data, 2);

        return slotCount;
    }

    /// <summary>
    /// Retrieves the row at the given slot number.
    ///
    /// 1. Read the slot at position HeaderSize + slotNumber * SlotSize.
    /// 2. If the row offset is 0, the row is deleted — return null.
    /// 3. Otherwise, copy rowLength bytes from _data starting at rowOffset into a new byte array.
    /// 4. Return the new byte array.
    /// </summary>
    public byte[]? GetRow(int slotNumber)
    {
        var slotPosition = HeaderSize + slotNumber * SlotSize;
        var rowOffset = BitConverter.ToUInt16(_data, slotPosition);
        if (rowOffset == 0)
        {
            return null;
        }

        var rowLength = BitConverter.ToUInt16(_data, slotPosition + 2);
        var data = new byte[rowLength];
        Array.Copy(_data, rowOffset, data, 0, rowLength);
        return data;
    }

    /// <summary>
    /// Marks a row as deleted by setting its slot's row offset to 0.
    /// Write 0 as UInt16 at position HeaderSize + slotNumber * SlotSize.
    /// </summary>
    public void DeleteRow(int slotNumber)
    {
        var slotPosition = HeaderSize + slotNumber * SlotSize;
        WriteToArray(BitConverter.GetBytes((short)0), _data, slotPosition);
    }

    /// <summary>
    /// Returns the underlying byte array (so it can be written back to disk via PageManager).
    /// </summary>
    public byte[] GetPageData()
    {
        return _data;
    }

    private static void WriteToArray(byte[] src, byte[] dst, int dstOffset)
    {
        Array.Copy(src, 0, dst, dstOffset, src.Length);
    }
}
