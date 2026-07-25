namespace TobbeSQL.Storage;

/// <summary>
/// Manages reading and writing fixed-size pages to a single file on disk.
/// Each page is PageSize bytes. Page N lives at byte offset N * PageSize in the file.
/// </summary>
public class PageManager : IDisposable
{
    public const int PageSize = 4096;
    private readonly FileStream _fileStream;

    public PageManager(string filePath)
    {
        _fileStream = File.Open(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
    }

    /// <summary>
    /// Returns how many pages the file currently holds.
    /// Calculate from the file's length divided by PageSize.
    /// </summary>
    public int PageCount
    {
        get { return (int)(_fileStream.Length / PageSize); }
    }

    /// <summary>
    /// Reads page number <paramref name="pageNumber"/> from the file.
    /// Seek to pageNumber * PageSize, then read PageSize bytes into a new byte array.
    /// Returns the byte array (always exactly PageSize bytes).
    /// </summary>
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

    /// <summary>
    /// Writes <paramref name="data"/> to page number <paramref name="pageNumber"/>.
    /// Seek to pageNumber * PageSize, then write exactly PageSize bytes.
    /// The data array must be exactly PageSize bytes long.
    /// </summary>
    public void WritePage(int pageNumber, byte[] data)
    {
        _fileStream.Seek(pageNumber * PageSize, SeekOrigin.Begin);
        _fileStream.Write(data);
        _fileStream.Flush();
    }

    /// <summary>
    /// Grows the file by one page and returns the new page's number.
    /// The new page number is the current PageCount (before growing).
    /// Write PageSize zero-bytes at the end of the file to extend it.
    /// Flush the stream so the file length is updated immediately.
    /// </summary>
    public int AllocatePage()
    {
        var currentPage = PageCount;
        _fileStream.Seek(0, SeekOrigin.End);
        _fileStream.Write(new byte[PageSize]);
        _fileStream.Flush();
        return currentPage;
    }

    /// <summary>
    /// Closes the underlying file stream.
    /// </summary>
    public void Dispose()
    {
        _fileStream.Dispose();
    }
}
