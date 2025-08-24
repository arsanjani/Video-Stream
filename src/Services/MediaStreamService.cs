using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using System.Data;
using System.Transactions;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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

    public bool IsValidId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        // Enforce trimmed identifiers
        if (id != id.Trim())
        {
            return false;
        }

        // Reasonable length limits for security
        if (id.Length > 50)
        {
            return false;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Windows: disallow runtime invalid filename chars plus control chars
            var runtimeInvalid = Path.GetInvalidFileNameChars();
            var controlChars = Enumerable.Range(0, 32).Select(i => (char)i);
            var invalidChars = runtimeInvalid.Concat(controlChars).Distinct().ToArray();
            if (id.IndexOfAny(invalidChars) >= 0)
            {
                return false;
            }

            // File names cannot end with a dot on Windows
            if (id.EndsWith('.'))
                return false;

            // Reserved device names (CON, PRN, AUX, NUL, COM1..COM9, LPT1..LPT9)
            // are not allowed even with extensions (e.g. "CON.txt")
            var reservedNames = new[] { "CON", "PRN", "AUX", "NUL" }
                .Concat(Enumerable.Range(1, 9).Select(i => "COM" + i))
                .Concat(Enumerable.Range(1, 9).Select(i => "LPT" + i))
                .ToArray();

            var firstPart = id.Split('.').FirstOrDefault()?.ToUpperInvariant() ?? string.Empty;
            if (reservedNames.Contains(firstPart))
                return false;
        }
        else
        {
            // Linux/Unix: only slash '/' and NUL/control characters are invalid in filenames
            var invalidChars = new[] { '/' }.Concat(Enumerable.Range(0, 32).Select(i => (char)i)).Distinct().ToArray();
            if (id.IndexOfAny(invalidChars) >= 0)
            {
                return false;
            }
        }

        return true;
    }

    public async Task<MediaStreamInfo?> GetMediaStreamInfoAsync(string id)
    {
        if (!IsValidId(id))
            return null;

        var cacheKey = $"MediaStream-{id}";
        
        if (_cache.TryGetValue(cacheKey, out MediaStreamInfo? cachedInfo))
        {
            return cachedInfo;
        }

        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand("SELECT FileSize, FileType, FileExt FROM MediaStream WHERE id = @id", connection);
            command.Parameters.AddWithValue("@id", id);

            using var reader = await command.ExecuteReaderAsync();
            
            if (!await reader.ReadAsync())
                return null;

            var mediaInfo = new MediaStreamInfo
            {
                Id = id,
                FileSize = reader.GetInt64("FileSize"),
                FileType = reader.GetString("FileType"),
                FileExt = reader.GetString("FileExt")
            };

            // Check if file exists in buffer paths
            foreach (var bufferPath in _bufferPaths)
            {
                var filePath = Path.Combine(bufferPath, id + mediaInfo.FileExt);
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
            _logger.LogError(ex, "Error retrieving media stream info for Id {Id}", id);
            return null;
        }
    }

    public async Task<Stream> CreatePartialContentAsync(string id, long start, long end)
    {
        var mediaInfo = await GetMediaStreamInfoAsync(id);
        if (mediaInfo == null)
            throw new FileNotFoundException($"Media stream not found for id: {id}");

        return await CreatePartialContentAsync(mediaInfo, start, end);
    }

    public Task<Stream> CreatePartialContentAsync(MediaStreamInfo mediaInfo, long start, long end)
    {
        if (mediaInfo == null)
            throw new ArgumentNullException(nameof(mediaInfo));

        if (mediaInfo.InBuffer && !string.IsNullOrEmpty(mediaInfo.BufferPath))
        {
            return CreatePartialContentFromBufferAsync(mediaInfo.BufferPath, start, end);
        }

        return CreatePartialContentFromSqlServerAsync(mediaInfo.Id, start, end);
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

    private async Task<Stream> CreatePartialContentFromSqlServerAsync(string id, long start, long end)
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
                    "SELECT SUBSTRING(FileData, @offset, @length) FROM MediaStream WHERE id = @id", 
                    connection);
                command.Parameters.AddWithValue("@id", id);
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
            _logger.LogError(ex, "Error creating partial content from SQL Server for Id {Id}", id);
            throw;
        }
    }
} 