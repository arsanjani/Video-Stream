using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Web;

namespace md.akharinkhabar.ir.Biz
{
    public static class HttpApiExtensions
    {
        public static CancellationToken GetCancellationToken(this HttpRequestMessage request)
        {
            CancellationToken cancellationToken;
            object value;
            var key = typeof(HttpApiExtensions).Namespace + ":CancellationToken";

            if (request.Properties.TryGetValue(key, out value))
            {
                return (CancellationToken)value;
            }

            var httpContext = HttpContext.Current;

            if (httpContext != null)
            {
                var httpResponse = httpContext.Response;

                if (httpResponse != null)
                {
                    try
                    {
                        cancellationToken = httpResponse.ClientDisconnectedToken;
                    }
                    catch
                    {
                        // Do not support cancellation.
                    }
                }
            }

            request.Properties[key] = cancellationToken;

            return cancellationToken;
        }
    }
}