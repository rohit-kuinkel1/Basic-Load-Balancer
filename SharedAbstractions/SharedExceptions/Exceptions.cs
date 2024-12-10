using System;

namespace LoadBalancer.Exceptions
{
    /// <summary>
    /// Base exception class for all load balancer related exceptions
    /// </summary>
    public class LoadBalancerException : Exception
    {
        public string ErrorCode { get; }
        public DateTime Timestamp { get; }

        public LoadBalancerException(string message)
            : base(message)
        {
            Timestamp = DateTime.UtcNow;
            ErrorCode = "LB-GEN-001";
        }

        public LoadBalancerException(string message, Exception innerException)
            : base(message, innerException)
        {
            Timestamp = DateTime.UtcNow;
            ErrorCode = "LB-GEN-001";
        }

        public LoadBalancerException(string message, string errorCode)
            : base(message)
        {
            Timestamp = DateTime.UtcNow;
            ErrorCode = errorCode;
        }

        public LoadBalancerException(string message, string errorCode, Exception innerException)
            : base(message, innerException)
        {
            Timestamp = DateTime.UtcNow;
            ErrorCode = errorCode;
        }
    }
}