using System;

namespace LoadBalancer.Exceptions
{
    /// <summary>
    /// Exception thrown when health checks fail for nodes in the load balancer
    /// </summary>
    public class HealthCheckException : LoadBalancerException
    {
        public string NodeId { get; }
        public DateTime LastSuccessfulCheck { get; }
        public int FailedAttempts { get; }

        public HealthCheckException(string nodeId, DateTime lastSuccessfulCheck, int failedAttempts, string message)
            : base(message, "LB-HEALTH-001")
        {
            NodeId = nodeId;
            LastSuccessfulCheck = lastSuccessfulCheck;
            FailedAttempts = failedAttempts;
        }

        public HealthCheckException(string nodeId, DateTime lastSuccessfulCheck, int failedAttempts, string message, Exception innerException)
            : base(message, "LB-HEALTH-001", innerException)
        {
            NodeId = nodeId;
            LastSuccessfulCheck = lastSuccessfulCheck;
            FailedAttempts = failedAttempts;
        }
    }
}