using System;

namespace LoadBalancer.Exceptions
{
    /// <summary>
    /// Exception thrown when there are issues connecting to a node in the load balancer
    /// </summary>
    public class ServerException : LoadBalancerException
    {
        public string NodeId { get; }
        public string NodeAddress { get; }

        public ServerException(string nodeId, string nodeAddress, string message)
            : base(message, "LB-SRV-001")
        {
            NodeId = nodeId;
            NodeAddress = nodeAddress;
        }

        public ServerException(string nodeId, string nodeAddress, string message, Exception innerException)
            : base(message, "LB-SRV-001", innerException)
        {
            NodeId = nodeId;
            NodeAddress = nodeAddress;
        }
    }
}