using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Prism.Middleware
{
    // You may need to install the Microsoft.AspNetCore.Http.Abstractions package into your project
    public class ForwardingMiddleware
    {        
        public ForwardingMiddleware(RequestDelegate next)
        {
            if (next != null)
            {
                // this is terminating middleware to keep upstream auth tied to the immediate send
            }
        }

        public async Task Invoke(HttpContext httpContext, UriForwardingTransformer uriTransformer, AuthProvider authProvider)
        {
            string currentHost = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";
            var response = httpContext.Response;
            
            Uri dest = uriTransformer.GetUriForActualHost(httpContext.Request.Path, httpContext.Request.QueryString.Value);
            bool canAuth = authProvider.CanAuthenticate(dest);

            var client = new HttpClient();
            var request = new HttpRequestMessage(new HttpMethod(httpContext.Request.Method), dest);
            if (canAuth)
            {
                request.Headers.Authorization = authProvider.GetAuthenticationHeader(dest, false);
            }

            var upstream = await client.SendAsync(request);
            if (upstream.StatusCode == System.Net.HttpStatusCode.Unauthorized && canAuth)
            {
                request.Headers.Authorization = authProvider.GetAuthenticationHeader(dest, true);
                upstream = await client.SendAsync(request);
            }

            foreach (var header in upstream.Headers.Concat(upstream.Content.Headers))
            {
                response.Headers.Add(header.Key, new StringValues(RedirectValues(header.Value, uriTransformer, currentHost).ToArray()));
            }

            response.Headers["x-Prism"] = "Passthru by your local friendly prism router";
            response.StatusCode = (int)upstream.StatusCode;

            if (response.ContentType.Contains("json") || response.ContentType.Contains("xml"))
            {
                var contents = await upstream.Content.ReadAsStringAsync();
                contents = uriTransformer.RedirectUrisToCurrentHost(contents, currentHost);

                var bytes = System.Text.Encoding.UTF8.GetBytes(contents);
                response.ContentLength = bytes.Length;
                response.Headers.ContentLength = bytes.Length;
                response.Body.Write(bytes);
            }
            else
            {
                await upstream.Content.CopyToAsync(response.Body);
            }
        }

        private IEnumerable<string> RedirectValues(IEnumerable<string> values, UriForwardingTransformer uriTransformer, string currentHost)
        {
            foreach(var value in values)
            {
                yield return (value.StartsWith("http"))
                    ? uriTransformer.RedirectUrisToCurrentHost(value, currentHost)
                    : value;
            }
        }
    }

    // Extension method used to add the middleware to the HTTP request pipeline.
    public static class ForwardingMiddlewareExtensions
    {
        public static IApplicationBuilder RunForwardingMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ForwardingMiddleware>();
        }
    }
}
