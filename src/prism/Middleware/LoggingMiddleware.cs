using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Prism.Middleware
{
    // You may need to install the Microsoft.AspNetCore.Http.Abstractions package into your project
    public class LoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly RequestLog _exchangeLog;

        public LoggingMiddleware(RequestDelegate next, RequestLog exchangeLog)
        {
            _next = next;
            _exchangeLog = exchangeLog;
        }

        public async Task Invoke(HttpContext httpContext, UriForwardingTransformer uriTransformer, TrackedRequestAccessor requestLog, ConnectionInfoFactory connectionFactory)
        {
            var connection = await connectionFactory.GetConnection();
            var request = new TrackedRequest(httpContext.Request, uriTransformer, connection);

            _exchangeLog.Add(request);
            requestLog.SetRequest(request);

            await _next(httpContext);

            request.Complete(new TrackedResponse(httpContext.Response));
        }
    }

    // Extension method used to add the middleware to the HTTP request pipeline.
    public static class LoggingMiddlewareExtensions
    {
        public static IApplicationBuilder UseLoggingMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<LoggingMiddleware>();
        }
    }
}
