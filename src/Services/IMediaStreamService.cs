using VideoStream.Models;

namespace VideoStream.Services;

public interface IMediaStreamService
{
    Task<MediaStreamInfo?> GetMediaStreamInfoAsync(string newsId);
    Task<Stream> CreatePartialContentAsync(string newsId, long start, long end);
    bool IsValidNewsId(string newsId);
} 