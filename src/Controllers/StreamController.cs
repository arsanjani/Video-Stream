using md.akharinkhabar.ir.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Runtime.Caching;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Hosting;
using System.Web.Http;

namespace md.akharinkhabar.ir.Controllers
{
    public class StreamController : ApiController
    {
        // We have a read-only dictionary for mapping file extensions and MIME names. 
        public static readonly IReadOnlyDictionary<string, string> MimeNames;
        private static ObjectCache cache = MemoryCache.Default;
        #region Constructors

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

        #endregion

        #region Actions
        public HttpResponseMessage Get(string newsid, CancellationToken cancellationToken)
        {
            try
            {
                if (newsid != null && long.TryParse(newsid, out long n))
                {
                    MediaStream streamline;
                    if (cache.Contains("MediaStream-" + newsid))
                        streamline = (MediaStream)cache.Get("MediaStream-" + newsid);
                    else
                    {
                        streamline = new MediaStream(newsid);
                        cache.Set("MediaStream-" + newsid, streamline, DateTime.Now.AddSeconds(30.0));
                    }
                    if (streamline.FileSize == 0)
                        throw new HttpResponseException(HttpStatusCode.NotFound);

                    RangeHeaderValue rangeHeader = Request.Headers.Range;
                    HttpResponseMessage response = new HttpResponseMessage();

                    response.Headers.AcceptRanges.Add("bytes");

                    // The request will be treated as normal request if there is no Range header.
                    if (rangeHeader == null || !rangeHeader.Ranges.Any())
                    {
                        response.StatusCode = HttpStatusCode.OK;
                        response.Content = new PushStreamContent(async (outputStream, httpContent, transpContext)
                        =>
                        {
                            using (outputStream)
                                await streamline.CreatePartialContent(outputStream, 0, streamline.FileSize, newsid);
                        }
                        , GetMimeNameFromExt(streamline.FileExt));

                        response.Content.Headers.ContentLength = streamline.FileSize;
                        response.Content.Headers.ContentType = GetMimeNameFromExt(streamline.FileExt);

                        return response;
                    }
                    long start = 0, end = 0;
                    // 1. If the unit is not 'bytes'.
                    // 2. If there are multiple ranges in header value.
                    // 3. If start or end position is greater than file length.
                    if (rangeHeader.Unit != "bytes" || rangeHeader.Ranges.Count > 1 ||
                        !TryReadRangeItem(rangeHeader.Ranges.First(), streamline.FileSize, out start, out end))
                    {
                        response.StatusCode = HttpStatusCode.RequestedRangeNotSatisfiable;
                        response.Content = new StreamContent(Stream.Null);  // No content for this status.
                        response.Content.Headers.ContentRange = new ContentRangeHeaderValue(streamline.FileSize);
                        response.Content.Headers.ContentType = GetMimeNameFromExt(streamline.FileExt);

                        return response;
                    }

                    var contentRange = new ContentRangeHeaderValue(start, end, streamline.FileSize);

                    // We are now ready to produce partial content.
                    response.StatusCode = HttpStatusCode.PartialContent;

                    response.Content = new PushStreamContent(async (outputStream, httpContent, transpContext)
                    =>
                    {
                        using (outputStream) // Copy the file to output stream in indicated range.
                            await streamline.CreatePartialContent(outputStream, start, end, newsid);

                    }, GetMimeNameFromExt(streamline.FileExt));
                    response.Content.Headers.ContentRange = contentRange;

                    return response;


                }
                else
                {
                    throw new HttpResponseException(HttpStatusCode.NotFound);
                }
            }
            catch (Exception ex)
            {
                
                throw ex;
            }
        } 
        #endregion

        #region Others
        private static MediaTypeHeaderValue GetMimeNameFromExt(string ext)
        {
            string value;

            if (MimeNames.TryGetValue(ext.ToLowerInvariant(), out value))
                return new MediaTypeHeaderValue(value);
            else
                return new MediaTypeHeaderValue(MediaTypeNames.Application.Octet);
        }

        private static  bool TryReadRangeItem(RangeItemHeaderValue range, long contentLength,
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
            return (start < contentLength && end < contentLength);
        }
        #endregion
    }
}
