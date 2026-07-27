namespace TobbeSQL.Storage;

/// <summary>
/// Page layout:
///   [Header (4 bytes)] [Slot array, growing forward] [... free space ...] [Row data, growing backward]
///
/// Header: bytes 0-1 = slot count (UInt16), bytes 2-3 = free space offset (UInt16).
/// Each slot (4 bytes): bytes 0-1 = row offset (0 = deleted), bytes 2-3 = row length.
/// </summary>
public class SlottedPage
{
    private const int HeaderSize = 4;
    private const int SlotSize = 4;

    private readonly byte[] _data;

    public SlottedPage(byte[] data)
    {
        _data = data;
    }

    public static void Initialize(byte[] page)
    {
        WriteToArray(BitConverter.GetBytes((short)0), page, 0);
        WriteToArray(BitConverter.GetBytes((short)PageManager.PageSize), page, 2);
    }

    public int SlotCount => BitConverter.ToUInt16(_data, 0);

    public int FreeSpace => BitConverter.ToUInt16(_data, 2) - (HeaderSize + SlotCount * SlotSize);

    public int InsertRow(byte[] rowData)
    {
        if (FreeSpace < rowData.Length + SlotSize)
        {
            return -1;
        }

        var slotCount = SlotCount;

        var freeSpaceOffset = BitConverter.ToUInt16(_data, 2);
        var offset = freeSpaceOffset - rowData.Length;
        WriteToArray(rowData, _data, offset);

        var slotPosition = HeaderSize + slotCount * SlotSize;
        WriteToArray(BitConverter.GetBytes((short)offset), _data, slotPosition);
        WriteToArray(BitConverter.GetBytes((short)rowData.Length), _data, slotPosition + 2);

        WriteToArray(BitConverter.GetBytes((short)(slotCount + 1)), _data, 0);
        WriteToArray(BitConverter.GetBytes((short)offset), _data, 2);

        return slotCount;
    }

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

    public void DeleteRow(int slotNumber)
    {
        var slotPosition = HeaderSize + slotNumber * SlotSize;
        WriteToArray(BitConverter.GetBytes((short)0), _data, slotPosition);
    }

    public byte[] GetPageData()
    {
        return _data;
    }

    private static void WriteToArray(byte[] src, byte[] dst, int dstOffset)
    {
        Array.Copy(src, 0, dst, dstOffset, src.Length);
    }
}
