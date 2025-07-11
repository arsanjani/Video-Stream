using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using System.Data;
using System.Transactions;
using VideoStream.Models;

namespace VideoStream.Services;

public class MediaStreamService : IMediaStreamService
{
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;
    private readonly ILogger<MediaStreamService> _logger;
    private readonly IFileSystemService _fileSystemService;
    private readonly string _connectionString;
    private readonly string[] _bufferPaths;
    private readonly int _readStreamBufferSize;
    private readonly int _cacheExpirationSeconds;

    public MediaStreamService(IConfiguration configuration, IMemoryCache cache, ILogger<MediaStreamService> logger, IFileSystemService fileSystemService)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _fileSystemService = fileSystemService ?? throw new ArgumentNullException(nameof(fileSystemService));
        _connectionString = configuration.GetConnectionString("Media") ?? throw new InvalidOperationException("Media connection string not found");
        _bufferPaths = configuration.GetSection("VideoStream:BufferPaths").Get<string[]>() ?? new[] { "B:\\", "F:\\" };
        _readStreamBufferSize = configuration.GetValue<int>("VideoStream:ReadStreamBufferSize", 262144);
        _cacheExpirationSeconds = configuration.GetValue<int>("VideoStream:CacheExpirationSeconds", 30);
    }

    public bool IsValidNewsId(string newsId)
    {
        if (string.IsNullOrWhiteSpace(newsId) || newsId != newsId.Trim())
        {
            return false;
        }

        if (long.TryParse(newsId, out long parsedId))
        {
            return parsedId >= 0;
        }

        return false;
    }

    public async Task<MediaStreamInfo?> GetMediaStreamInfoAsync(string newsId)
    {
        if (!IsValidNewsId(newsId))
            return null;

        var cacheKey = $"MediaStream-{newsId}";
        
        if (_cache.TryGetValue(cacheKey, out MediaStreamInfo? cachedInfo))
        {
            return cachedInfo;
        }

        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand("SELECT FileSize, FileType, FileExt FROM MediaStream WHERE newsid = @newsId", connection);
            command.Parameters.AddWithValue("@newsId", newsId);

            using var reader = await command.ExecuteReaderAsync();
            
            if (!await reader.ReadAsync())
                return null;

            var mediaInfo = new MediaStreamInfo
            {
                Id = newsId,
                FileSize = reader.GetInt64("FileSize"),
                FileType = reader.GetString("FileType"),
                FileExt = reader.GetString("FileExt")
            };

            // Check if file exists in buffer paths
            foreach (var bufferPath in _bufferPaths)
            {
                var filePath = Path.Combine(bufferPath, newsId + mediaInfo.FileExt);
                if (_fileSystemService.FileExists(filePath))
                {
                    if (_fileSystemService.CanOpenFile(filePath))
                    {
                        mediaInfo.BufferPath = filePath;
                        mediaInfo.InBuffer = true;
                        break;
                    }
                }
            }

            // Cache the result
            _cache.Set(cacheKey, mediaInfo, TimeSpan.FromSeconds(_cacheExpirationSeconds));
            
            return mediaInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving media stream info for newsId {NewsId}", newsId);
            return null;
        }
    }

    public async Task<Stream> CreatePartialContentAsync(string newsId, long start, long end)
    {
        var mediaInfo = await GetMediaStreamInfoAsync(newsId);
        if (mediaInfo == null)
            throw new FileNotFoundException($"Media stream not found for newsId: {newsId}");

        if (mediaInfo.InBuffer && !string.IsNullOrEmpty(mediaInfo.BufferPath))
        {
            return await CreatePartialContentFromBufferAsync(mediaInfo.BufferPath, start, end);
        }
        else
        {
            return await CreatePartialContentFromSqlServerAsync(newsId, start, end);
        }
    }

    private Task<Stream> CreatePartialContentFromBufferAsync(string bufferPath, long start, long end)
    {
        try
        {
            var fileStream = _fileSystemService.OpenRead(bufferPath, _readStreamBufferSize);
            // Don't set position here - let PartialStream handle it
            
            return Task.FromResult<Stream>(new PartialStream(fileStream, start, end));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating partial content from buffer {BufferPath}", bufferPath);
            throw;
        }
    }

    private async Task<Stream> CreatePartialContentFromSqlServerAsync(string newsId, long start, long end)
    {
        try
        {
            // Since SqlFileStream is deprecated, we'll use a different approach
            // We'll read the data in chunks from the database
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            // Create a memory stream to hold the data
            var memoryStream = new MemoryStream();
            
            // Read the file data in chunks
            var chunkSize = Math.Min(_readStreamBufferSize, (int)(end - start + 1));
            var buffer = new byte[chunkSize];
            var currentPosition = start;
            
            while (currentPosition <= end)
            {
                var bytesToRead = Math.Min(chunkSize, (int)(end - currentPosition + 1));
                
                // Use SUBSTRING to read a portion of the FILESTREAM data
                // Note: This approach may not be as efficient as SqlFileStream for very large files
                using var command = new SqlCommand(
                    "SELECT SUBSTRING(FileData, @offset, @length) FROM MediaStream WHERE newsid = @newsId", 
                    connection);
                command.Parameters.AddWithValue("@newsId", newsId);
                command.Parameters.AddWithValue("@offset", currentPosition + 1); // SQL Server uses 1-based indexing
                command.Parameters.AddWithValue("@length", bytesToRead);

                var data = await command.ExecuteScalarAsync() as byte[];
                if (data == null || data.Length == 0)
                    break;

                await memoryStream.WriteAsync(data, 0, data.Length);
                currentPosition += data.Length;
            }

            memoryStream.Position = 0;
            return memoryStream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating partial content from SQL Server for newsId {NewsId}", newsId);
            throw;
        }
    }
} 