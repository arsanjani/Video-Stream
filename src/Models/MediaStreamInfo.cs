namespace VideoStream.Models;

public class MediaStreamInfo
{
    public string Id { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string FileType { get; set; } = string.Empty;
    public string FileExt { get; set; } = string.Empty;
    public string? BufferPath { get; set; }
    public bool InBuffer { get; set; }
} 