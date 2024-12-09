namespace LoadBalancer
{
    public class CircuitBreaker
    {
        private readonly CircuitBreakerConfig _config;
        private readonly object _stateLock = new();
        private volatile CircuitStates _state;
        private int _failureCount;
        private DateTime _lastFailureTime;
        private int _halfOpenAttempts;

        public CircuitBreaker( CircuitBreakerConfig config )
        {
            _config = config ?? throw new ArgumentNullException( nameof( config ) );
            _state = CircuitStates.Closed;
            _failureCount = 0;
            _lastFailureTime = DateTime.MinValue;
        }

        public CircuitStates State
        {
            get => _state;
            internal set => _state = value;
        }

        public bool AllowRequest()
        {
            lock( _stateLock )
            {
                switch( _state )
                {
                    case CircuitStates.Closed:
                        return true;

                    case CircuitStates.Open:
                        if( DateTime.UtcNow - _lastFailureTime >= _config.ResetTimeoutSec )
                        {
                            State = CircuitStates.HalfOpen;
                            _halfOpenAttempts = 0;
                            return true;
                        }
                        return false;

                    case CircuitStates.HalfOpen:
                        if( _halfOpenAttempts < _config.HalfOpenMaxAttempts )
                        {
                            Interlocked.Increment( ref _halfOpenAttempts );
                            return true;
                        }
                        return false;

                    default:
                        return false;
                }
            }
        }

        public void RecordSuccess()
        {
            lock( _stateLock )
            {
                switch( _state )
                {
                    case CircuitStates.HalfOpen:
                        Reset();
                        break;
                    case CircuitStates.Closed:
                        _failureCount = 0;
                        break;
                }
            }
        }

        public void RecordFailure()
        {
            lock( _stateLock )
            {
                switch( _state )
                {
                    case CircuitStates.HalfOpen:
                        Trip();
                        break;
                    case CircuitStates.Closed:
                        _failureCount++;
                        if( _failureCount >= _config.FailureThreshold )
                        {
                            Trip();
                        }
                        break;
                }
            }
        }

        private void Trip()
        {
            State = CircuitStates.Open;
            _lastFailureTime = DateTime.UtcNow;
            _failureCount = 0;
        }

        public void Reset()
        {
            State = CircuitStates.Closed;
            _failureCount = 0;
            _halfOpenAttempts = 0;
            _lastFailureTime = DateTime.MinValue;
        }
    }
}
