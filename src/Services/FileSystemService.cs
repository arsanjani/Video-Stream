namespace VideoStream.Services;

/// <summary>
/// Implementation of file system operations
/// </summary>
public class FileSystemService : IFileSystemService
{
    private readonly ILogger<FileSystemService> _logger;

    public FileSystemService(ILogger<FileSystemService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public bool FileExists(string path)
    {
        try
        {
            return File.Exists(path);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking if file exists: {FilePath}", path);
            return false;
        }
    }

    /// <inheritdoc />
    public FileStream OpenRead(string path, int bufferSize)
    {
        try
        {
            return new FileStream(path, FileMode.Open, FileAccess.Read, 
                FileShare.ReadWrite | FileShare.Delete, bufferSize, FileOptions.Asynchronous);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening file for reading: {FilePath}", path);
            throw;
        }
    }

    /// <inheritdoc />
    public bool CanOpenFile(string path)
    {
        try
        {
            using var testStream = File.OpenRead(path);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cannot open file: {FilePath}", path);
            return false;
        }
    }
} 