using LoadBalancer.Exceptions;
namespace LoadBalancer
{
    public record AutoScalingConfig
    {
        public int MinServers { get; init; } = 2;
        public int MaxServers { get; init; } = 4;
        public int NumberOfMaxRequestForScaleUp { get; init; } = 100;
        public int NumberOfMinRequestForScaleDown { get; init; } = 20;
        public TimeSpan ScaleCheckIntervalMs { get; init; } = TimeSpan.FromMilliseconds( 100 );

        public static AutoScalingConfig Default => new();

        public static AutoScalingConfig Factory(
            int? minServers = null,
            int? maxServers = null,
            int? requestThresholdForScaleUp = null,
            int? requestThresholdForScaleDown = null,
            int? scaleCheckIntervalMs = null )
        {
            return new()
            {
                MinServers = minServers ?? 2,
                MaxServers = maxServers ?? 4,
                NumberOfMaxRequestForScaleUp = requestThresholdForScaleUp ?? 100,
                NumberOfMinRequestForScaleDown = requestThresholdForScaleDown ?? 20,
                ScaleCheckIntervalMs = TimeSpan.FromMilliseconds( scaleCheckIntervalMs ?? 100 )
            };
        }

        public AutoScalingConfig WithMinServers( int minServers )
            => this with { MinServers = minServers };

        public AutoScalingConfig WithMaxServers( int maxServers )
            => this with { MaxServers = maxServers };

        public AutoScalingConfig WithScaleUpThreshold( int threshold )
            => this with { NumberOfMaxRequestForScaleUp = threshold };

        public AutoScalingConfig WithScaleDownThreshold( int threshold )
            => this with { NumberOfMinRequestForScaleDown = threshold };

        public AutoScalingConfig WithScaleCheckInterval( TimeSpan interval )
            => this with { ScaleCheckIntervalMs = interval };

        public void Validate()
        {
            if( MinServers < 0 )
            {
                throw new ArgumentException( "MinServers cannot be negative", nameof( MinServers ) );
            }

            if( MaxServers < MinServers )
            {
                throw new ArgumentException( $"{nameof(MaxServers)} must be greater than or equal to {nameof(MinServers)}" );
            }

            if( NumberOfMaxRequestForScaleUp <= 0 )
            {
                throw new ArgumentException( $"{nameof(NumberOfMaxRequestForScaleUp)} threshold must be positive", nameof( NumberOfMaxRequestForScaleUp ) );
            }

            if( NumberOfMinRequestForScaleDown < 0 )
            {
                throw new ArgumentException( $"{nameof( NumberOfMinRequestForScaleDown )} cannot be negative", nameof( NumberOfMinRequestForScaleDown ) );
            }
        }
    }
}