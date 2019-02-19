using Microsoft.AspNetCore.Http;
using System;
using System.Text.RegularExpressions;

namespace Prism
{
    public class UriForwardingTransformer
    {
        private Regex _urlRegex = new Regex("https://(([a-z0-9_.-]+)?(\\.visualstudio.com|dev\\.azure\\.com))", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public Uri GetUriForActualHost(PathString currentHostPath, string queryString)
        {
            if (!currentHostPath.StartsWithSegments("/uri", StringComparison.OrdinalIgnoreCase, out PathString remaining) || !remaining.HasValue)
            {
                throw new InvalidRequestException("Forwarding path must begin with /uri/ and contain a redirect path");
            }

            var builder = new UriBuilder("https://" + remaining.Value.Substring(1));
            builder.Query = queryString;

            return builder.Uri;
        }

        public string RedirectUrisToCurrentHost(string contents, string currentHost)
        {
            return _urlRegex.Replace(contents, match =>
            {
                return $"{currentHost}/uri/" + match.Groups[1].Value;
            });
        }
    }
}
