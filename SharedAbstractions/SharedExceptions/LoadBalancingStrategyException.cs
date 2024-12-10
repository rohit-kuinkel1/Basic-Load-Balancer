using System;

namespace LoadBalancer.Exceptions
{
    /// <summary>
    /// Exception thrown when there are issues with the load balancing strategy
    /// </summary>
    public class LoadBalancingStrategyException : LoadBalancerException
    {
        public string StrategyName { get; }
        public int ActiveNodes { get; }

        public LoadBalancingStrategyException(string strategyName, int activeNodes, string message)
            : base(message, "LB-STRAT-001")
        {
            StrategyName = strategyName;
            ActiveNodes = activeNodes;
        }

        public LoadBalancingStrategyException(string strategyName, int activeNodes, string message, Exception innerException)
            : base(message, "LB-STRAT-001", innerException)
        {
            StrategyName = strategyName;
            ActiveNodes = activeNodes;
        }
    }
}