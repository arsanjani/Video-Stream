namespace VideoStream.Services;

/// <summary>
/// Interface for file system operations to improve testability
/// </summary>
public interface IFileSystemService
{
    /// <summary>
    /// Checks if a file exists at the specified path
    /// </summary>
    /// <param name="path">The file path to check</param>
    /// <returns>True if the file exists, false otherwise</returns>
    bool FileExists(string path);

    /// <summary>
    /// Opens a file for reading
    /// </summary>
    /// <param name="path">The file path to open</param>
    /// <param name="bufferSize">The buffer size for the file stream</param>
    /// <returns>A file stream for reading</returns>
    FileStream OpenRead(string path, int bufferSize);

    /// <summary>
    /// Tests if a file can be opened for reading
    /// </summary>
    /// <param name="path">The file path to test</param>
    /// <returns>True if the file can be opened, false otherwise</returns>
    bool CanOpenFile(string path);
} 