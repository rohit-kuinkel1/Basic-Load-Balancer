namespace LoadBalancer.Exceptions
{
    /// <summary>
    /// Base exception class for all load balancer related exceptions
    /// </summary>
    public class LoadBalancerException : Exception
    {
        public string ErrorCode { get; }
        public DateTime Timestamp { get; }
        public string? ExceptionType { get; }

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

        public LoadBalancerException(string message, Type exception, string? errorCode)
            : base(message)
        {
            Timestamp = DateTime.UtcNow;
            ErrorCode = errorCode ?? "LB-ARG-001";
        }

        public LoadBalancerException(string message, string errorCode, Exception innerException)
            : base(message, innerException)
        {
            Timestamp = DateTime.UtcNow;
            ErrorCode = errorCode;
        }

        /// <summary>
        /// Constructs a LoadBalancerException with a message, error code, exception type, and inner exception
        /// </summary>
        /// <param name="message">The error message</param>
        /// <param name="errorCode">A specific error code for the exception</param>
        /// <param name="exceptionType">The string representation of the exception type</param>
        /// <param name="innerException">The inner exception that caused this exception</param>
        public LoadBalancerException(string message, string errorCode, string exceptionType, Exception innerException)
            : base(message, innerException)
        {
            Timestamp = DateTime.UtcNow;
            ErrorCode = errorCode;
            ExceptionType = exceptionType;
        }

        /// <summary>
        /// Provides a detailed string representation of the exception
        /// </summary>
        /// <returns>A string containing exception details</returns>
        public override string ToString()
        {
            var baseString = base.ToString();

            var details = $"Load Balancer Exception Details:\n" +
                          $"Message: {Message}\n" +
                          $"Error Code: {ErrorCode}\n" +
                          $"Timestamp: {Timestamp}\n";

            if (!string.IsNullOrEmpty(ExceptionType))
            {
                details += $"Exception Type: {ExceptionType}\n";
            }

            if (InnerException != null)
            {
                details += $"Inner Exception: {InnerException.GetType().Name}\n" +
                           $"Inner Exception Message: {InnerException.Message}";
            }

            return details;
        }
    }
}