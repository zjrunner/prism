using Microsoft.AspNetCore.Http;
using System;
using System.Threading;

namespace Prism
{
    public abstract class TrackedExchange
    {
        public IHeaderDictionary Headers { get; }
        public DateTimeOffset Time { get; }

        protected TrackedExchange(IHeaderDictionary headers)
        {
            Time = DateTimeOffset.UtcNow;
            Headers = headers;
        }
    }

    public class TrackedRequest : TrackedExchange
    {
        private static long _globalOrder = 0;

        public Guid Id { get; }
        public long Order { get; }
        public Uri OriginalUri { get; }
        public Uri Uri { get; }
        public string Method { get; }
        public TimeSpan Duration { get; private set; }
        public string Session { get; }
        public TrackedResponse Response { get; private set; }

        public TrackedRequest(HttpRequest request, UriForwardingTransformer uriTransformer, ClientConnectionInfo connection)
            : base(request.Headers)
        {
            Order = Interlocked.Increment(ref _globalOrder);
            Id = Guid.NewGuid();
            OriginalUri = new Uri(Microsoft.AspNetCore.Http.Extensions.UriHelper.GetEncodedUrl(request));
            Uri = uriTransformer.GetUriForActualHost(request.Path, request.QueryString.Value);
            Method = request.Method;
            Session = connection.Session;
        }

        public void Complete(TrackedResponse response)
        {
            Duration = response.Time.Subtract(Time);
            Response = response;
        }
    }

    public class TrackedResponse : TrackedExchange
    {
        public int StatusCode { get; }

        public TrackedResponse(HttpResponse response)
            : base(response.Headers)
        {
            StatusCode = response.StatusCode;
        }
    }


    // just playin around with scopes
    public class TrackedRequestAccessor
    {
        private TrackedRequest _request;

        public void SetRequest(TrackedRequest request)
        {
            if (_request != null)
            {
                throw new InvalidOperationException("Can't set a tracked request twice");
            }

            _request = request;
        }

        public TrackedRequest GetRequest()
        {
            if (_request == null)
            {
                throw new InvalidOperationException("No request yet");
            }

            return _request;
        }
    }
}
