using Microsoft.AspNetCore.Mvc;
using System.Net.Mime;
using System.Collections.ObjectModel;
using VideoStream.Services;

namespace VideoStream.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StreamController : ControllerBase
    {
        private readonly IMediaStreamService _mediaStreamService;
        private readonly ILogger<StreamController> _logger;
        
        // We have a read-only dictionary for mapping file extensions and MIME names. 
        public static readonly IReadOnlyDictionary<string, string> MimeNames;

        static StreamController()
        {
            var mimeNames = new Dictionary<string, string>
            {
                { ".mp3", "audio/mpeg" },// List all supported media types; 
                { ".mp4", "video/mp4" },
                { ".ogg", "application/ogg" },
                { ".ogv", "video/ogg" },
                { ".oga", "audio/ogg" },
                { ".wav", "audio/x-wav" },
                { ".webm", "video/webm" }
            };

            MimeNames = new ReadOnlyDictionary<string, string>(mimeNames);
        }

        public StreamController(IMediaStreamService mediaStreamService, ILogger<StreamController> logger)
        {
            _mediaStreamService = mediaStreamService ?? throw new ArgumentNullException(nameof(mediaStreamService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet("{id}")]
        [HttpHead("{id}")]
        public async Task<IActionResult> Get(string id, CancellationToken cancellationToken)
        {
            try
            {
                if (!_mediaStreamService.IsValidId(id))
                {
                    return NotFound();
                }

                var mediaInfo = await _mediaStreamService.GetMediaStreamInfoAsync(id);
                if (mediaInfo == null || mediaInfo.FileSize == 0)
                {
                    return NotFound();
                }

                var rangeHeader = Request.GetTypedHeaders().Range;
                
                // Set accept ranges header
                Response.Headers.AcceptRanges = "bytes";

                // The request will be treated as normal request if there is no Range header.
                if (rangeHeader == null || !rangeHeader.Ranges.Any())
                {
                    var fullContentStream = await _mediaStreamService.CreatePartialContentAsync(id, 0, mediaInfo.FileSize - 1);
                    return File(fullContentStream, GetMimeNameFromExt(mediaInfo.FileExt), enableRangeProcessing: true);
                }

                long start = 0, end = 0;
                // 1. If the unit is not 'bytes'.
                // 2. If there are multiple ranges in header value.
                // 3. If start or end position is greater than file length.
                if (rangeHeader.Unit != "bytes" || rangeHeader.Ranges.Count > 1 ||
                    !TryReadRangeItem(rangeHeader.Ranges.First(), mediaInfo.FileSize, out start, out end))
                {
                    Response.Headers.ContentRange = $"bytes */{mediaInfo.FileSize}";
                    return StatusCode(416); // Range Not Satisfiable
                }

                // Create partial content stream
                var partialContentStream = await _mediaStreamService.CreatePartialContentAsync(id, start, end);

                // Set content range and length headers for HTTP/2 correctness
                var contentLength = partialContentStream.CanSeek ? partialContentStream.Length : (end - start + 1);
                var servedEnd = start + contentLength - 1;
                Response.Headers.ContentRange = $"bytes {start}-{servedEnd}/{mediaInfo.FileSize}";
                Response.ContentLength = contentLength;

                // Return 206 Partial Content
                Response.StatusCode = 206;
                return new FileStreamResult(partialContentStream, GetMimeNameFromExt(mediaInfo.FileExt))
                {
                    EnableRangeProcessing = false
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error streaming media for Id {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpOptions("{id}")]
        public IActionResult Options(string id)
        {
            Response.Headers["Allow"] = "GET, HEAD, OPTIONS";
            Response.Headers["Access-Control-Allow-Origin"] = "*";
            Response.Headers["Access-Control-Allow-Methods"] = "GET, HEAD, OPTIONS";
            Response.Headers["Access-Control-Allow-Headers"] = "Range, Content-Type";
            return Ok();
        }

        private static string GetMimeNameFromExt(string ext)
        {
            if (MimeNames.TryGetValue(ext.ToLowerInvariant(), out string? value))
                return value;
            else
                return MediaTypeNames.Application.Octet;
        }

        private static bool TryReadRangeItem(Microsoft.Net.Http.Headers.RangeItemHeaderValue range, long contentLength,
           out long start, out long end)
        {
            if (range.From != null)
            {
                start = range.From.Value;
                if (range.To != null)
                    end = range.To.Value;
                else
                    end = contentLength - 1;
            }
            else
            {
                end = contentLength - 1;
                if (range.To != null)
                    start = contentLength - range.To.Value;
                else
                    start = 0;
            }
            
            return (start >= 0 && start < contentLength && end >= start && end <= contentLength - 1);
        }
    }
}
