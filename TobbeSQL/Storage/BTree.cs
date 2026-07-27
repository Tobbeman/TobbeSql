namespace TobbeSQL.Storage;

/// <summary>
/// A B-tree index stored on pages via a PageManager.
///
/// The B-tree maps integer keys to RowIds, enabling fast lookups.
/// Each node occupies one page. The tree starts with a single leaf root
/// and grows upward as nodes split.
///
/// Splitting:
///   When a leaf is full and we need to insert:
///     1. Allocate a new page for the right sibling
///     2. Move the upper half of entries to the right sibling
///     3. The "split key" (first key of right sibling) is promoted to the parent
///     4. If the parent is full, split it recursively
///     5. If the root splits, allocate a new root with one key and two children
/// </summary>
public class BTree
{
    private readonly PageManager _pageManager;

    /// <summary>
    /// Opens an existing B-tree. The root is always page 0.
    /// </summary>
    public BTree(PageManager pageManager)
    {
        _pageManager = pageManager;
    }

    /// <summary>
    /// Creates a new, empty B-tree. Allocates page 0 as the root (an empty leaf).
    /// Returns the new BTree instance.
    /// </summary>
    public static BTree Create(PageManager pageManager)
    {
        var rootPage = pageManager.AllocatePage();
        var data = pageManager.ReadPage(rootPage);
        BTreeNode.InitializeLeaf(data);
        pageManager.WritePage(rootPage, data);
        return new BTree(pageManager);
    }

    /// <summary>
    /// Searches for all RowIds matching the given key.
    ///
    /// Steps:
    ///   1. Start at the root node
    ///   2. If internal: use FindChild(key) to descend to the correct child, repeat
    ///   3. If leaf: scan all entries and collect those where entry.key == key
    ///   4. Return the list of matching RowIds
    ///
    /// Note: There may be duplicate keys (multiple rows with the same indexed value),
    /// so collect all matches, not just the first.
    /// </summary>
    public List<RowId> Search(int key)
    {
        var pageNumber = 0;
        var rows = new List<RowId>();
        BTreeNode node;
        while (true)
        {
            var page = _pageManager.ReadPage(pageNumber);
            node = new BTreeNode(page);

            if (node.IsLeaf)
            {
                break;
            }
            pageNumber = node.FindChild(key);
        }

        for (var i = 0; i < node.KeyCount; i++)
        {
            if (key == node.GetLeafKey(i))
            {
                rows.Add(node.GetLeafRowId(i));
            }
        }

        return rows;
    }

    /// <summary>
    /// Inserts a key/RowId pair into the B-tree.
    ///
    /// Steps:
    ///   1. Find the correct leaf by descending from the root (tracking the path of parent nodes)
    ///   2. If the leaf is not full, insert directly and write the page
    ///   3. If the leaf is full, split it:
    ///      a. Allocate a new page for the right leaf
    ///      b. Move the upper half of entries to the right leaf
    ///      c. Insert the new entry into whichever leaf it belongs in
    ///      d. Promote the split key (first key of right leaf) to the parent
    ///   4. If promoting to the parent causes it to be full, split the parent too (recursively)
    ///   5. If the root itself splits, create a new root
    ///
    /// The "path" is the stack of (pageNumber, node) pairs visited from root to leaf.
    /// After a split, walk back up the path inserting the promoted key into each parent.
    /// </summary>
    public void Insert(int key, RowId rowId)
    {
        var pageNumber = 0;
        var path = new List<(int PageNumber, BTreeNode Node)>();
        BTreeNode node;

        while (true)
        {
            var page = _pageManager.ReadPage(pageNumber);
            node = new BTreeNode(page);
            path.Add((pageNumber, node));
            if (node.IsLeaf)
            {
                break;
            }
            pageNumber = node.FindChild(key);
        }

        if (!node.IsLeafFull)
        {
            node.InsertLeafEntry(key, rowId);
            _pageManager.WritePage(path[^1].PageNumber, node.GetPageData());
            return;
        }

        var newPage = _pageManager.AllocatePage();
        var newNodeData = _pageManager.ReadPage(newPage);
        BTreeNode.InitializeLeaf(newNodeData);
        var newNode = new BTreeNode(newNodeData);

        var splitPoint = node.KeyCount / 2;
        for (var i = splitPoint; i < node.KeyCount; i++)
        {
            newNode.InsertLeafEntry(node.GetLeafKey(i), node.GetLeafRowId(i));
        }
        node.KeyCount = splitPoint;

        // Insert the new entry into the correct side
        if (key < newNode.GetLeafKey(0))
        {
            node.InsertLeafEntry(key, rowId);
        }
        else
        {
            newNode.InsertLeafEntry(key, rowId);
        }

        // Write both leaves
        _pageManager.WritePage(path[^1].PageNumber, node.GetPageData());
        _pageManager.WritePage(newPage, newNode.GetPageData());

        // Promote the split key up through parents
        var promoteKey = newNode.GetLeafKey(0);
        var newChildPage = newPage;

        // Walk back up the path (skip the leaf at the end)
        for (var i = path.Count - 2; i >= 0; i--)
        {
            var (parentPageNum, parentNode) = path[i];

            if (!parentNode.IsInternalFull)
            {
                parentNode.InsertInternalEntry(promoteKey, newChildPage);
                _pageManager.WritePage(parentPageNum, parentNode.GetPageData());
                return;
            }

            // Internal node is full — split without overflowing the page buffer.
            // Collect all existing keys + the new promote key into a sorted list,
            // then distribute: left gets first half, middle key promotes up, right gets second half.
            var allKeys = new List<(int Key, int RightChild)>();
            for (var j = 0; j < parentNode.KeyCount; j++)
            {
                allKeys.Add((parentNode.GetInternalKey(j), parentNode.GetInternalChild(j + 1)));
            }

            // Insert the new key in sorted order
            var insertPos = 0;
            while (insertPos < allKeys.Count && allKeys[insertPos].Key < promoteKey)
            {
                insertPos++;
            }
            allKeys.Insert(insertPos, (promoteKey, newChildPage));

            // Split: left gets [0..midIndex-1], middle promotes, right gets [midIndex+1..end]
            var midIndex = allKeys.Count / 2;
            var midKey = allKeys[midIndex].Key;

            // Rebuild left node (reuse parentNode's page)
            var leftFirstChild = parentNode.GetInternalChild(0);
            parentNode.KeyCount = 0;
            BitConverter.GetBytes(leftFirstChild).CopyTo(parentNode.GetPageData(), 3);
            for (var j = 0; j < midIndex; j++)
            {
                parentNode.InsertInternalEntry(allKeys[j].Key, allKeys[j].RightChild);
            }

            // Build right node
            var newInternalPageNum = _pageManager.AllocatePage();
            var newInternalData = _pageManager.ReadPage(newInternalPageNum);
            newInternalData[0] = 0; // IsLeaf = false
            BitConverter.GetBytes((ushort)0).CopyTo(newInternalData, 1); // KeyCount = 0
            var newInternalNode = new BTreeNode(newInternalData);

            // Right node's first child is the right child of the middle key
            BitConverter.GetBytes(allKeys[midIndex].RightChild).CopyTo(newInternalData, 3);
            for (var j = midIndex + 1; j < allKeys.Count; j++)
            {
                newInternalNode.InsertInternalEntry(allKeys[j].Key, allKeys[j].RightChild);
            }

            _pageManager.WritePage(parentPageNum, parentNode.GetPageData());
            _pageManager.WritePage(newInternalPageNum, newInternalNode.GetPageData());

            promoteKey = midKey;
            newChildPage = newInternalPageNum;
        }

        // Root split: copy current root (page 0) to a new page, then overwrite page 0
        var copyPageNum = _pageManager.AllocatePage();
        var rootData = _pageManager.ReadPage(0);
        _pageManager.WritePage(copyPageNum, rootData);

        // The left child is the copy, right child is newChildPage
        var newRootData = new byte[PageManager.PageSize];
        BTreeNode.InitializeInternal(newRootData, promoteKey, copyPageNum, newChildPage);
        _pageManager.WritePage(0, newRootData);
    }

    /// <summary>
    /// Deletes a key/RowId pair from the B-tree.
    ///
    /// Simple implementation (no rebalancing/merging):
    ///   1. Find the correct leaf by descending from the root
    ///   2. Call RemoveLeafEntry(key, rowId) on the leaf
    ///   3. Write the page back
    ///
    /// Note: A production B-tree would merge underfull nodes, but for this lesson
    /// we skip that — deleted entries just leave space in the leaf.
    /// </summary>
    public void Delete(int key, RowId rowId)
    {
        var pageNumber = 0;
        BTreeNode node;
        while (true)
        {
            var page = _pageManager.ReadPage(pageNumber);
            node = new BTreeNode(page);

            if (node.IsLeaf)
            {
                break;
            }
            pageNumber = node.FindChild(key);
        }

        node.RemoveLeafEntry(key, rowId);
        _pageManager.WritePage(pageNumber, node.GetPageData());
    }
}
