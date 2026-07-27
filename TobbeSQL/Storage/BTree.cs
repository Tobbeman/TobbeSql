namespace TobbeSQL.Storage;

public class BTree
{
    private readonly PageManager _pageManager;

    public BTree(PageManager pageManager)
    {
        _pageManager = pageManager;
    }

    public static BTree Create(PageManager pageManager)
    {
        var rootPage = pageManager.AllocatePage();
        var data = pageManager.ReadPage(rootPage);
        BTreeNode.InitializeLeaf(data);
        pageManager.WritePage(rootPage, data);
        return new BTree(pageManager);
    }

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

        if (key < newNode.GetLeafKey(0))
        {
            node.InsertLeafEntry(key, rowId);
        }
        else
        {
            newNode.InsertLeafEntry(key, rowId);
        }

        _pageManager.WritePage(path[^1].PageNumber, node.GetPageData());
        _pageManager.WritePage(newPage, newNode.GetPageData());

        var promoteKey = newNode.GetLeafKey(0);
        var newChildPage = newPage;

        for (var i = path.Count - 2; i >= 0; i--)
        {
            var (parentPageNum, parentNode) = path[i];

            if (!parentNode.IsInternalFull)
            {
                parentNode.InsertInternalEntry(promoteKey, newChildPage);
                _pageManager.WritePage(parentPageNum, parentNode.GetPageData());
                return;
            }

            var allKeys = new List<(int Key, int RightChild)>();
            for (var j = 0; j < parentNode.KeyCount; j++)
            {
                allKeys.Add((parentNode.GetInternalKey(j), parentNode.GetInternalChild(j + 1)));
            }

            var insertPos = 0;
            while (insertPos < allKeys.Count && allKeys[insertPos].Key < promoteKey)
            {
                insertPos++;
            }
            allKeys.Insert(insertPos, (promoteKey, newChildPage));

            var midIndex = allKeys.Count / 2;
            var midKey = allKeys[midIndex].Key;

            var leftFirstChild = parentNode.GetInternalChild(0);
            parentNode.KeyCount = 0;
            BitConverter.GetBytes(leftFirstChild).CopyTo(parentNode.GetPageData(), 3);
            for (var j = 0; j < midIndex; j++)
            {
                parentNode.InsertInternalEntry(allKeys[j].Key, allKeys[j].RightChild);
            }

            var newInternalPageNum = _pageManager.AllocatePage();
            var newInternalData = _pageManager.ReadPage(newInternalPageNum);
            newInternalData[0] = 0;
            BitConverter.GetBytes((ushort)0).CopyTo(newInternalData, 1);
            var newInternalNode = new BTreeNode(newInternalData);

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

        var copyPageNum = _pageManager.AllocatePage();
        var rootData = _pageManager.ReadPage(0);
        _pageManager.WritePage(copyPageNum, rootData);

        var newRootData = new byte[PageManager.PageSize];
        BTreeNode.InitializeInternal(newRootData, promoteKey, copyPageNum, newChildPage);
        _pageManager.WritePage(0, newRootData);
    }

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
