using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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
        private static HttpClient _client;

        static ForwardingMiddleware()
        {
            var handler = new HttpClientHandler();
            handler.AllowAutoRedirect = false;
            handler.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;
            handler.PreAuthenticate = true;
            //handler.MaxConnectionsPerServer = 50;
            //handler.Proxy = new WebProxy("localhost", 8888);
            //handler.UseProxy = true;
            _client = new HttpClient(handler);
        }

        public ForwardingMiddleware(RequestDelegate next)
        {
            if (next != null)
            {
                // this is terminating middleware to keep upstream auth tied to the immediate send
            }
        }

        public async Task Invoke(HttpContext httpContext, UriForwardingTransformer uriTransformer, AuthProvider authProvider, ConnectionInfoFactory connectionFactory)
        {
            var requestLeft = httpContext.Request;
            var responseLeft = httpContext.Response;
            var clientConnection = await connectionFactory.GetConnection();

            string currentHost = $"{requestLeft.Scheme}://{requestLeft.Host}";
            Uri dest = uriTransformer.GetUriForActualHost(requestLeft.Path, requestLeft.QueryString.Value);

            var requestRight = GetRequest(requestLeft.Method, dest, requestLeft.Headers, clientConnection.Session);

            var canAuth = authProvider.CanAuthenticate(dest, clientConnection);
            if (canAuth)
            {
                requestRight.Headers.Authorization = authProvider.GetAuthenticationHeader(dest, clientConnection, false);
            }
            
            var responseRight = await _client.SendAsync(requestRight);
            if (responseRight.StatusCode == HttpStatusCode.Unauthorized && canAuth)
            {
                requestRight = GetRequest(requestLeft.Method, dest, requestLeft.Headers, clientConnection.Session);
                requestRight.Headers.Authorization = authProvider.GetAuthenticationHeader(dest, clientConnection, true);
                responseRight = await _client.SendAsync(requestRight);
            }

            foreach (var header in responseRight.Headers.Concat(responseRight.Content.Headers))
            {
                Console.WriteLine($"Got header [{header.Key}] with [{header.Value}]");
                if (header.Key == "Accept-Encoding") { continue; }
                responseLeft.Headers.Add(header.Key, new StringValues(RedirectValues(header.Value, uriTransformer, currentHost).ToArray()));
            }

            responseLeft.Headers["X-Prism-SessionId"] = clientConnection.Session;
            responseLeft.StatusCode = (int)responseRight.StatusCode;

            if (responseLeft.ContentType.Contains("json") || responseLeft.ContentType.Contains("xml"))
            {
                var contents = await responseRight.Content.ReadAsStringAsync();
                contents = uriTransformer.RedirectUrisToCurrentHost(contents, currentHost);

                var bytes = System.Text.Encoding.UTF8.GetBytes(contents);
                responseLeft.ContentLength = bytes.Length;
                responseLeft.Headers.ContentLength = bytes.Length;
                responseLeft.Body.Write(bytes);
            }
            else
            {
                await responseRight.Content.CopyToAsync(responseLeft.Body);
            }
        }

        private HttpRequestMessage GetRequest(string method, Uri dest, IHeaderDictionary headers, string session)
        {
            var request = new HttpRequestMessage(new HttpMethod(method), dest);
            
            foreach (var header in headers)
            {
                var values = header.Value.ToList();
                switch (header.Key)
                {
                    case "User-Agent":
                    {
                        values.Add("(prism.1.0.0)");
                        request.Headers.Add(header.Key, values);
                        break;
                    }
                    case "Connection":
                    case "Host":
                    case "Accept-Encoding":
                        break;
                    default:
                        {
                            request.Headers.Add(header.Key, values);
                            break;
                        }
                }
            }
            
            request.Headers.Add("X-Prism-SessionId", session);
            return request; 
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
