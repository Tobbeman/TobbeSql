namespace TobbeSQL.Storage;

/// <summary>
/// Represents a single node in the B-tree, stored as one page (4096 bytes).
///
/// Page layout:
///   Byte 0:       IsLeaf flag (1 = leaf, 0 = internal)
///   Bytes 1-2:    Key count (ushort, little-endian)
///
/// For LEAF nodes (stores keys + RowIds):
///   Entry layout (repeating from byte 3):
///     [4 bytes: key (int)] [4 bytes: RowId.PageNumber (int)] [2 bytes: RowId.SlotNumber (ushort)]
///   Each entry is 10 bytes. Max entries per leaf = (4096 - 3) / 10 = 409
///
/// For INTERNAL nodes (stores keys + child page pointers):
///   Layout from byte 3:
///     [4 bytes: child0 page number]
///     Then repeating: [4 bytes: key] [4 bytes: child page number]
///   So for N keys there are N+1 children.
///   Each key+child pair is 8 bytes, plus the initial child pointer (4 bytes).
///   Max keys per internal node = (4096 - 3 - 4) / 8 = 511
///
/// The B-tree invariant:
///   - For internal node with keys [k0, k1, ..., kN-1]:
///     child0 contains all keys < k0
///     child1 contains all keys >= k0 and < k1
///     ...
///     childN contains all keys >= kN-1
/// </summary>
public class BTreeNode
{
    public const int MaxLeafEntries = 409;
    public const int MaxInternalKeys = 511;

    private const int IsLeafOffset = 0;
    private const int KeyCountOffset = 1;
    private const int DataOffset = 3;

    // Entry sizes
    private const int LeafEntrySize = 10; // 4 (key) + 4 (page) + 2 (slot)
    private const int InternalPairSize = 8; // 4 (key) + 4 (child pointer)

    private readonly byte[] _data;

    /// <summary>
    /// Wraps an existing page byte array as a BTreeNode.
    /// The caller is responsible for reading/writing the page via PageManager.
    /// </summary>
    public BTreeNode(byte[] pageData)
    {
        _data = pageData;
    }

    /// <summary>
    /// Returns true if this node is a leaf, false if it's an internal node.
    /// Read from byte 0 of the page.
    /// </summary>
    public bool IsLeaf => _data[IsLeafOffset] == 1;

    /// <summary>
    /// The number of keys currently stored in this node.
    /// Read/write from bytes 1-2 as a ushort.
    /// </summary>
    public int KeyCount
    {
        get => BitConverter.ToUInt16(_data, KeyCountOffset);
        internal set => BitConverter.GetBytes((ushort)value).CopyTo(_data, KeyCountOffset);
    }

    /// <summary>
    /// Returns the raw page data (for writing back via PageManager.WritePage).
    /// </summary>
    public byte[] GetPageData() => _data;

    /// <summary>
    /// Initializes a page as an empty leaf node.
    /// Sets IsLeaf = 1, KeyCount = 0.
    /// </summary>
    public static void InitializeLeaf(byte[] pageData)
    {
        pageData[IsLeafOffset] = 1;
        BitConverter.GetBytes((ushort)0).CopyTo(pageData, KeyCountOffset);
    }

    /// <summary>
    /// Initializes a page as an internal node with one key and two children.
    /// Used when the root splits.
    ///
    /// Sets IsLeaf = 0, KeyCount = 1, then writes:
    ///   child0 (leftPageNumber), key, child1 (rightPageNumber)
    /// </summary>
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

    // --- Leaf node operations ---

    /// <summary>
    /// Gets the key at the given index in a leaf node.
    /// Offset = DataOffset + index * LeafEntrySize
    /// </summary>
    public int GetLeafKey(int index)
    {
        return BitConverter.ToInt32(_data, DataOffset + index * LeafEntrySize);
    }

    /// <summary>
    /// Gets the RowId at the given index in a leaf node.
    /// Page number is at offset + 4, slot number at offset + 8.
    /// </summary>
    public RowId GetLeafRowId(int index)
    {
        var offset = DataOffset + index * LeafEntrySize;
        var pageNumber = BitConverter.ToInt32(_data, offset + 4);
        var slotNumber = BitConverter.ToUInt16(_data, offset + 8);
        return new RowId(pageNumber, slotNumber);
    }

    /// <summary>
    /// Inserts a key/RowId pair into this leaf node at the correct sorted position.
    ///
    /// Steps:
    ///   1. Find the insertion index (first position where existing key >= new key)
    ///   2. Shift all entries from that index onward to the right by one entry
    ///   3. Write the new key and RowId at the insertion index
    ///   4. Increment KeyCount
    ///
    /// Assumes the caller has already checked that the node is not full.
    /// </summary>
    public void InsertLeafEntry(int key, RowId rowId)
    {
        var count = KeyCount;
        var insertAt = 0;
        while (insertAt < count && GetLeafKey(insertAt) < key)
        {
            insertAt++;
        }

        // Shift entries right
        for (var i = count - 1; i >= insertAt; i--)
        {
            var src = DataOffset + i * LeafEntrySize;
            var dst = DataOffset + (i + 1) * LeafEntrySize;
            Buffer.BlockCopy(_data, src, _data, dst, LeafEntrySize);
        }

        // Write new entry
        var offset = DataOffset + insertAt * LeafEntrySize;
        BitConverter.GetBytes(key).CopyTo(_data, offset);
        BitConverter.GetBytes(rowId.PageNumber).CopyTo(_data, offset + 4);
        BitConverter.GetBytes((ushort)rowId.SlotNumber).CopyTo(_data, offset + 8);

        KeyCount = count + 1;
    }

    /// <summary>
    /// Removes a key/RowId pair from this leaf node.
    ///
    /// Steps:
    ///   1. Find the entry matching both the key and the RowId
    ///   2. Shift all entries after it to the left by one
    ///   3. Decrement KeyCount
    ///
    /// Returns true if found and removed, false otherwise.
    /// </summary>
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
                    // Shift entries left
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

    /// <summary>
    /// Returns true if this leaf node is full (cannot accept another entry).
    /// </summary>
    public bool IsLeafFull => KeyCount >= MaxLeafEntries;

    // --- Internal node operations ---

    /// <summary>
    /// Gets the child page number at the given child index in an internal node.
    /// child0 is at DataOffset, then each subsequent child is after a key.
    ///
    /// Layout: [child0] [key0] [child1] [key1] [child2] ...
    /// child index i: DataOffset + i * InternalPairSize  (for i=0: just DataOffset)
    ///
    /// Actually: child0 at DataOffset, then pairs of (key, child) follow.
    /// So childN is at DataOffset + N * InternalPairSize for N >= 0... but wait:
    ///   child0: DataOffset
    ///   key0:   DataOffset + 4
    ///   child1: DataOffset + 8
    ///   key1:   DataOffset + 12
    ///   child2: DataOffset + 16
    ///
    /// Pattern: child[i] is at DataOffset + i * 8, key[i] is at DataOffset + 4 + i * 8
    /// </summary>
    public int GetInternalChild(int childIndex)
    {
        return BitConverter.ToInt32(_data, DataOffset + childIndex * InternalPairSize);
    }

    /// <summary>
    /// Gets the key at the given index in an internal node.
    /// key[i] is at DataOffset + 4 + i * 8
    /// </summary>
    public int GetInternalKey(int index)
    {
        return BitConverter.ToInt32(_data, DataOffset + 4 + index * InternalPairSize);
    }

    /// <summary>
    /// Finds which child to follow for the given search key.
    ///
    /// Walk through the keys: if searchKey < key[i], go to child[i].
    /// If searchKey >= all keys, go to child[keyCount].
    ///
    /// Returns the child page number.
    /// </summary>
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

    /// <summary>
    /// Inserts a new key and right-child pointer into this internal node.
    /// Called after a child split: the new key separates the old child (already at childIndex)
    /// from the new right child.
    ///
    /// Steps:
    ///   1. Find insertion position (first key > newKey)
    ///   2. Shift keys and children to the right
    ///   3. Write newKey at the position and newChildPageNumber as the child to its right
    ///   4. Increment KeyCount
    /// </summary>
    public void InsertInternalEntry(int key, int newChildPageNumber)
    {
        var count = KeyCount;
        var insertAt = 0;
        while (insertAt < count && GetInternalKey(insertAt) < key)
        {
            insertAt++;
        }

        // Shift from the end: move key[i] and child[i+1] rightward
        for (var i = count - 1; i >= insertAt; i--)
        {
            // Move key[i] to key[i+1]
            var srcKey = DataOffset + 4 + i * InternalPairSize;
            var dstKey = DataOffset + 4 + (i + 1) * InternalPairSize;
            Buffer.BlockCopy(_data, srcKey, _data, dstKey, 4);

            // Move child[i+1] to child[i+2]
            var srcChild = DataOffset + (i + 1) * InternalPairSize;
            var dstChild = DataOffset + (i + 2) * InternalPairSize;
            Buffer.BlockCopy(_data, srcChild, _data, dstChild, 4);
        }

        // Write new key and child
        BitConverter.GetBytes(key).CopyTo(_data, DataOffset + 4 + insertAt * InternalPairSize);
        BitConverter
            .GetBytes(newChildPageNumber)
            .CopyTo(_data, DataOffset + (insertAt + 1) * InternalPairSize);

        KeyCount = count + 1;
    }

    /// <summary>
    /// Returns true if this internal node is full (cannot accept another key).
    /// </summary>
    public bool IsInternalFull => KeyCount >= MaxInternalKeys;
}
