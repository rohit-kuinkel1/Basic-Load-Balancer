using LoadBalancer.Exceptions;
namespace LoadBalancer
{
    public record AutoScalingConfig
    {
        public int MinServers { get; init; } = 2;
        public int MaxServers { get; init; } = 4;
        public int NumberOfTotalMaxRequestForScaleUp { get; init; } = 100;
        public int NumberOfTotalMinRequestForScaleDown { get; init; } = 20;
        public TimeSpan ScaleCheckIntervalMs { get; init; } = TimeSpan.FromMilliseconds( 1000 );

        public static AutoScalingConfig Default => new();

        public static AutoScalingConfig Factory(
            int? minServers = null,
            int? maxServers = null,
            int? requestThresholdForScaleUp = null,
            int? requestThresholdForScaleDown = null,
            int? scaleCheckIntervalMs = null )
        {
            return new AutoScalingConfig()
            {
                MinServers = minServers ?? 2,
                MaxServers = maxServers ?? 4,
                NumberOfTotalMaxRequestForScaleUp = requestThresholdForScaleUp ?? 100,
                NumberOfTotalMinRequestForScaleDown = requestThresholdForScaleDown ?? 20,
                ScaleCheckIntervalMs = TimeSpan.FromMilliseconds( scaleCheckIntervalMs ?? 1000 )
            };
        }

        public AutoScalingConfig WithMinServers( int minServers )
            => this with { MinServers = minServers };

        public AutoScalingConfig WithMaxServers( int maxServers )
            => this with { MaxServers = maxServers };

        public AutoScalingConfig WithScaleUpThreshold( int threshold )
            => this with { NumberOfTotalMaxRequestForScaleUp = threshold };

        public AutoScalingConfig WithScaleDownThreshold( int threshold )
            => this with { NumberOfTotalMinRequestForScaleDown = threshold };

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

            if( NumberOfTotalMaxRequestForScaleUp <= 0 )
            {
                throw new ArgumentException( $"{nameof(NumberOfTotalMaxRequestForScaleUp)} threshold must be positive", nameof( NumberOfTotalMaxRequestForScaleUp ) );
            }

            if( NumberOfTotalMinRequestForScaleDown < 0 )
            {
                throw new ArgumentException( $"{nameof( NumberOfTotalMinRequestForScaleDown )} cannot be negative", nameof( NumberOfTotalMinRequestForScaleDown ) );
            }
        }
    }
}