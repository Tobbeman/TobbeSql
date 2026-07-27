namespace TobbeSQL.Storage;

public class PageManager : IDisposable
{
    public const int PageSize = 4096;
    private readonly FileStream _fileStream;

    public PageManager(string filePath)
    {
        _fileStream = File.Open(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
    }

    public int PageCount => (int)(_fileStream.Length / PageSize);

    public byte[] ReadPage(int pageNumber)
    {
        var bytes = new byte[PageSize];
        _fileStream.Seek(pageNumber * PageSize, SeekOrigin.Begin);
        var readBytes = _fileStream.Read(bytes);

        if (readBytes != PageSize)
        {
            throw new Exception($"Read the wrong amount of bytes: {pageNumber}, {readBytes}");
        }

        return bytes;
    }

    public void WritePage(int pageNumber, byte[] data)
    {
        _fileStream.Seek(pageNumber * PageSize, SeekOrigin.Begin);
        _fileStream.Write(data);
        _fileStream.Flush();
    }

    public int AllocatePage()
    {
        var currentPage = PageCount;
        _fileStream.Seek(0, SeekOrigin.End);
        _fileStream.Write(new byte[PageSize]);
        _fileStream.Flush();
        return currentPage;
    }

    public void Dispose()
    {
        _fileStream.Dispose();
    }
}
