using System;

namespace LoadBalancer.Exceptions
{
    /// <summary>
    /// Exception thrown when request throttling limits are exceeded
    /// </summary>
    public class ThrottlingException : LoadBalancerException
    {
        public int CurrentRequests { get; }
        public int MaxRequests { get; }
        public TimeSpan ThrottlingPeriod { get; }

        public ThrottlingException(int currentRequests, int maxRequests, TimeSpan throttlingPeriod, string message)
            : base(message, "LB-THROT-001")
        {
            CurrentRequests = currentRequests;
            MaxRequests = maxRequests;
            ThrottlingPeriod = throttlingPeriod;
        }

        public ThrottlingException(int currentRequests, int maxRequests, TimeSpan throttlingPeriod, string message, Exception innerException)
            : base(message, "LB-THROT-001", innerException)
        {
            CurrentRequests = currentRequests;
            MaxRequests = maxRequests;
            ThrottlingPeriod = throttlingPeriod;
        }
    }
}