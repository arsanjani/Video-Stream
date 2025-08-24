using VideoStream.Models;

namespace VideoStream.Services;

public interface IMediaStreamService
{
    Task<MediaStreamInfo?> GetMediaStreamInfoAsync(string id);
    Task<Stream> CreatePartialContentAsync(string id, long start, long end);
    Task<Stream> CreatePartialContentAsync(MediaStreamInfo mediaInfo, long start, long end);
    bool IsValidId(string id);
} 