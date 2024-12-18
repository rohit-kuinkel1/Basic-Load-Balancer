using System;

namespace LoadBalancer.RequestCache
{
    public class CachedRequest
    {
        public HttpRequestMessage Request { get; }
        public DateTime Timestamp { get; }

        public CachedRequest( HttpRequestMessage request )
        {
            Request = request;
            Timestamp = DateTime.UtcNow;
        }
    }
}