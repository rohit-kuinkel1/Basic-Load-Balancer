namespace Load_Balancer
{
    public class CircuitBreaker
    {
        private readonly CircuitBreakerConfig _config;
        private readonly object _stateLock = new();
        private volatile CircuitState _state;
        private int _failureCount;
        private DateTime _lastFailureTime;
        private int _halfOpenAttempts;

        public CircuitBreaker( CircuitBreakerConfig config )
        {
            _config = config ?? throw new ArgumentNullException( nameof( config ) );
            _state = CircuitState.Closed;
            _failureCount = 0;
            _lastFailureTime = DateTime.MinValue;
        }

        public CircuitState State
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
                    case CircuitState.Closed:
                        return true;

                    case CircuitState.Open:
                        if( DateTime.UtcNow - _lastFailureTime >= _config.ResetTimeout )
                        {
                            State = CircuitState.HalfOpen;
                            _halfOpenAttempts = 0;
                            return true;
                        }
                        return false;

                    case CircuitState.HalfOpen:
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
                    case CircuitState.HalfOpen:
                        Reset();
                        break;
                    case CircuitState.Closed:
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
                    case CircuitState.HalfOpen:
                        Trip();
                        break;
                    case CircuitState.Closed:
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
            State = CircuitState.Open;
            _lastFailureTime = DateTime.UtcNow;
            _failureCount = 0;
        }

        public void Reset()
        {
            State = CircuitState.Closed;
            _failureCount = 0;
            _halfOpenAttempts = 0;
            _lastFailureTime = DateTime.MinValue;
        }
    }
}
