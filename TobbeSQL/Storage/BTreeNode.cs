namespace TobbeSQL.Storage;

/// <summary>
/// A single B-tree node stored as one page (4096 bytes).
///
/// Layout:
///   Byte 0: IsLeaf (1 = leaf, 0 = internal), Bytes 1-2: KeyCount (ushort)
///
/// Leaf entries (from byte 3): [4B key] [4B RowId.Page] [2B RowId.Slot] = 10 bytes each
/// Internal layout (from byte 3): [4B child0] then repeating [4B key] [4B child] = 8 bytes per pair
/// </summary>
public class BTreeNode
{
    public const int MaxLeafEntries = 409;
    public const int MaxInternalKeys = 511;

    private const int IsLeafOffset = 0;
    private const int KeyCountOffset = 1;
    private const int DataOffset = 3;

    private const int LeafEntrySize = 10;
    private const int InternalPairSize = 8;

    private readonly byte[] _data;

    public BTreeNode(byte[] pageData)
    {
        _data = pageData;
    }

    public bool IsLeaf => _data[IsLeafOffset] == 1;

    public int KeyCount
    {
        get => BitConverter.ToUInt16(_data, KeyCountOffset);
        internal set => BitConverter.GetBytes((ushort)value).CopyTo(_data, KeyCountOffset);
    }

    public byte[] GetPageData() => _data;

    public static void InitializeLeaf(byte[] pageData)
    {
        pageData[IsLeafOffset] = 1;
        BitConverter.GetBytes((ushort)0).CopyTo(pageData, KeyCountOffset);
    }

    public static void InitializeInternal(
        byte[] pageData,
        int key,
        int leftPageNumber,
        int rightPageNumber
    )
    {
        pageData[IsLeafOffset] = 0;
        BitConverter.GetBytes((ushort)1).CopyTo(pageData, KeyCountOffset);
        BitConverter.GetBytes(leftPageNumber).CopyTo(pageData, DataOffset);
        BitConverter.GetBytes(key).CopyTo(pageData, DataOffset + 4);
        BitConverter.GetBytes(rightPageNumber).CopyTo(pageData, DataOffset + 8);
    }

    public int GetLeafKey(int index)
    {
        return BitConverter.ToInt32(_data, DataOffset + index * LeafEntrySize);
    }

    public RowId GetLeafRowId(int index)
    {
        var offset = DataOffset + index * LeafEntrySize;
        var pageNumber = BitConverter.ToInt32(_data, offset + 4);
        var slotNumber = BitConverter.ToUInt16(_data, offset + 8);
        return new RowId(pageNumber, slotNumber);
    }

    public void InsertLeafEntry(int key, RowId rowId)
    {
        var count = KeyCount;
        var insertAt = 0;
        while (insertAt < count && GetLeafKey(insertAt) < key)
        {
            insertAt++;
        }

        for (var i = count - 1; i >= insertAt; i--)
        {
            var src = DataOffset + i * LeafEntrySize;
            var dst = DataOffset + (i + 1) * LeafEntrySize;
            Buffer.BlockCopy(_data, src, _data, dst, LeafEntrySize);
        }

        var offset = DataOffset + insertAt * LeafEntrySize;
        BitConverter.GetBytes(key).CopyTo(_data, offset);
        BitConverter.GetBytes(rowId.PageNumber).CopyTo(_data, offset + 4);
        BitConverter.GetBytes((ushort)rowId.SlotNumber).CopyTo(_data, offset + 8);

        KeyCount = count + 1;
    }

    public bool RemoveLeafEntry(int key, RowId rowId)
    {
        var count = KeyCount;
        for (var i = 0; i < count; i++)
        {
            if (GetLeafKey(i) == key)
            {
                var rid = GetLeafRowId(i);
                if (rid.PageNumber == rowId.PageNumber && rid.SlotNumber == rowId.SlotNumber)
                {
                    for (var j = i; j < count - 1; j++)
                    {
                        var src = DataOffset + (j + 1) * LeafEntrySize;
                        var dst = DataOffset + j * LeafEntrySize;
                        Buffer.BlockCopy(_data, src, _data, dst, LeafEntrySize);
                    }
                    KeyCount = count - 1;
                    return true;
                }
            }
        }
        return false;
    }

    public bool IsLeafFull => KeyCount >= MaxLeafEntries;

    public int GetInternalChild(int childIndex)
    {
        return BitConverter.ToInt32(_data, DataOffset + childIndex * InternalPairSize);
    }

    public int GetInternalKey(int index)
    {
        return BitConverter.ToInt32(_data, DataOffset + 4 + index * InternalPairSize);
    }

    public int FindChild(int searchKey)
    {
        var count = KeyCount;
        for (var i = 0; i < count; i++)
        {
            if (searchKey < GetInternalKey(i))
            {
                return GetInternalChild(i);
            }
        }
        return GetInternalChild(count);
    }

    public void InsertInternalEntry(int key, int newChildPageNumber)
    {
        var count = KeyCount;
        var insertAt = 0;
        while (insertAt < count && GetInternalKey(insertAt) < key)
        {
            insertAt++;
        }

        for (var i = count - 1; i >= insertAt; i--)
        {
            var srcKey = DataOffset + 4 + i * InternalPairSize;
            var dstKey = DataOffset + 4 + (i + 1) * InternalPairSize;
            Buffer.BlockCopy(_data, srcKey, _data, dstKey, 4);

            var srcChild = DataOffset + (i + 1) * InternalPairSize;
            var dstChild = DataOffset + (i + 2) * InternalPairSize;
            Buffer.BlockCopy(_data, srcChild, _data, dstChild, 4);
        }

        BitConverter.GetBytes(key).CopyTo(_data, DataOffset + 4 + insertAt * InternalPairSize);
        BitConverter
            .GetBytes(newChildPageNumber)
            .CopyTo(_data, DataOffset + (insertAt + 1) * InternalPairSize);

        KeyCount = count + 1;
    }

    public bool IsInternalFull => KeyCount >= MaxInternalKeys;
}
