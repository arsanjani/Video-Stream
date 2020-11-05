using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace md.akharinkhabar.ir.Handler
{
    public class StreamMessageHandler : DelegatingHandler
    {
        protected async override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            try
            {
                var response = await base.SendAsync(request, cancellationToken);
                return response;
            }
            catch (Exception ex)
            {
                return request.CreateErrorResponse(System.Net.HttpStatusCode.InternalServerError, ex);                 
            }
        }
    }
}