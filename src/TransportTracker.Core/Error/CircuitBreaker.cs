using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace TransportTracker.Core.Error
{
    /// <summary>
    /// Circuit breaker state
    /// </summary>
    public enum CircuitState
    {
        /// <summary>
        /// Circuit is closed - operations execute normally
        /// </summary>
        Closed,
        
        /// <summary>
        /// Circuit is open - operations fail fast without execution
        /// </summary>
        Open,
        
        /// <summary>
        /// Circuit is half-open - testing if service has recovered
        /// </summary>
        HalfOpen
    }

    /// <summary>
    /// Implements the Circuit Breaker pattern to prevent repeated failure of operations
    /// </summary>
    public class CircuitBreaker : IDisposable
    {
        private readonly ILogger _logger;
        private readonly string _name;
        private readonly int _failureThreshold;
        private readonly TimeSpan _resetTimeout;
        private readonly object _stateLock = new();
        
        private int _failureCount;
        private CircuitState _state = CircuitState.Closed;
        private DateTime _lastStateChange = DateTime.Now;
        private Timer _resetTimer;

        /// <summary>
        /// Gets the current state of the circuit
        /// </summary>
        public CircuitState State
        {
            get { lock (_stateLock) { return _state; } }
            private set { lock (_stateLock) { _state = value; } }
        }

        /// <summary>
        /// Gets the number of consecutive failures
        /// </summary>
        public int FailureCount => _failureCount;

        /// <summary>
        /// Gets or sets the name of the circuit
        /// </summary>
        public string Name => _name;

        /// <summary>
        /// Event raised when the circuit state changes
        /// </summary>
        public event EventHandler<CircuitStateChangedEventArgs> StateChanged;

        /// <summary>
        /// Creates a new instance of CircuitBreaker
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="name">Name of the circuit</param>
        /// <param name="failureThreshold">Number of failures before opening the circuit</param>
        /// <param name="resetTimeoutSeconds">Time in seconds after which to try closing the circuit</param>
        public CircuitBreaker(
            ILogger logger,
            string name,
            int failureThreshold = 3,
            int resetTimeoutSeconds = 60)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _name = name ?? throw new ArgumentNullException(nameof(name));
            _failureThreshold = failureThreshold;
            _resetTimeout = TimeSpan.FromSeconds(resetTimeoutSeconds);
            
            _logger.LogInformation($"Circuit breaker '{_name}' initialized with threshold {_failureThreshold} failures and reset timeout {_resetTimeout.TotalSeconds}s");
        }

        /// <summary>
        /// Executes an operation through the circuit breaker
        /// </summary>
        /// <typeparam name="T">Return type of the operation</typeparam>
        /// <param name="operation">Operation to execute</param>
        /// <param name="fallback">Optional fallback function if circuit is open</param>
        /// <returns>Result of the operation or fallback</returns>
        /// <exception cref="CircuitBreakerOpenException">Thrown when circuit is open and no fallback provided</exception>
        public T Execute<T>(Func<T> operation, Func<T> fallback = null)
        {
            ThrowIfDisposed();

            if (!CanExecute())
            {
                if (fallback != null)
                {
                    _logger.LogDebug($"Circuit '{_name}' is {State}, using fallback");
                    return fallback();
                }
                
                throw new CircuitBreakerOpenException($"Circuit '{_name}' is open");
            }

            try
            {
                T result = operation();
                RecordSuccess();
                return result;
            }
            catch (Exception ex)
            {
                RecordFailure(ex);
                
                if (fallback != null)
                {
                    _logger.LogWarning(ex, $"Operation through circuit '{_name}' failed, using fallback");
                    return fallback();
                }
                
                throw;
            }
        }

        /// <summary>
        /// Executes an asynchronous operation through the circuit breaker
        /// </summary>
        /// <typeparam name="T">Return type of the operation</typeparam>
        /// <param name="operation">Async operation to execute</param>
        /// <param name="fallback">Optional async fallback function if circuit is open</param>
        /// <returns>Task with result of the operation or fallback</returns>
        /// <exception cref="CircuitBreakerOpenException">Thrown when circuit is open and no fallback provided</exception>
        public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, Func<Task<T>> fallback = null)
        {
            ThrowIfDisposed();

            if (!CanExecute())
            {
                if (fallback != null)
                {
                    _logger.LogDebug($"Circuit '{_name}' is {State}, using async fallback");
                    return await fallback();
                }
                
                throw new CircuitBreakerOpenException($"Circuit '{_name}' is open");
            }

            try
            {
                T result = await operation();
                RecordSuccess();
                return result;
            }
            catch (Exception ex)
            {
                RecordFailure(ex);
                
                if (fallback != null)
                {
                    _logger.LogWarning(ex, $"Async operation through circuit '{_name}' failed, using fallback");
                    return await fallback();
                }
                
                throw;
            }
        }

        /// <summary>
        /// Records a successful operation, potentially closing the circuit
        /// </summary>
        public void RecordSuccess()
        {
            lock (_stateLock)
            {
                _failureCount = 0;
                
                if (_state == CircuitState.HalfOpen)
                {
                    ChangeState(CircuitState.Closed);
                    _logger.LogInformation($"Circuit '{_name}' closed after successful test");
                    CancelResetTimer();
                }
            }
        }

        /// <summary>
        /// Records a failed operation, potentially opening the circuit
        /// </summary>
        /// <param name="exception">The exception that caused the failure</param>
        public void RecordFailure(Exception exception)
        {
            lock (_stateLock)
            {
                _failureCount++;
                
                if (_state == CircuitState.HalfOpen || 
                    (_state == CircuitState.Closed && _failureCount >= _failureThreshold))
                {
                    ChangeState(CircuitState.Open);
                    _logger.LogWarning(exception, $"Circuit '{_name}' opened after {_failureCount} failures");
                    SetResetTimer();
                }
            }
        }

        /// <summary>
        /// Manually resets the circuit to closed state
        /// </summary>
        public void Reset()
        {
            lock (_stateLock)
            {
                CancelResetTimer();
                _failureCount = 0;
                ChangeState(CircuitState.Closed);
                _logger.LogInformation($"Circuit '{_name}' manually reset to closed state");
            }
        }

        /// <summary>
        /// Disposes resources used by the circuit breaker
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes resources used by the circuit breaker
        /// </summary>
        /// <param name="disposing">True if called from Dispose()</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    CancelResetTimer();
                }

                _disposed = true;
            }
        }

        private bool _disposed;

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(CircuitBreaker));
            }
        }

        /// <summary>
        /// Checks if an operation can be executed
        /// </summary>
        private bool CanExecute()
        {
            lock (_stateLock)
            {
                if (_state == CircuitState.Closed)
                {
                    return true;
                }

                if (_state == CircuitState.Open)
                {
                    // Check if we should transition to half-open based on elapsed time
                    TimeSpan elapsedTime = DateTime.Now - _lastStateChange;
                    if (elapsedTime >= _resetTimeout)
                    {
                        ChangeState(CircuitState.HalfOpen);
                        _logger.LogInformation($"Circuit '{_name}' transitioned to half-open state after {elapsedTime.TotalSeconds:F1}s");
                        return true;
                    }
                    
                    return false;
                }

                // In half-open state, only allow one test operation
                return true;
            }
        }

        /// <summary>
        /// Changes the circuit state
        /// </summary>
        private void ChangeState(CircuitState newState)
        {
            if (_state == newState) return;

            var oldState = _state;
            _state = newState;
            _lastStateChange = DateTime.Now;
            
            StateChanged?.Invoke(this, new CircuitStateChangedEventArgs(
                _name, oldState, newState, _lastStateChange, _failureCount));
        }

        /// <summary>
        /// Sets up a timer to try resetting the circuit
        /// </summary>
        private void SetResetTimer()
        {
            CancelResetTimer();
            
            _resetTimer = new Timer(
                _ => TryTransitionToHalfOpen(),
                null,
                _resetTimeout,
                TimeSpan.FromMilliseconds(-1)); // Do not repeat
        }

        /// <summary>
        /// Cancels the reset timer
        /// </summary>
        private void CancelResetTimer()
        {
            _resetTimer?.Dispose();
            _resetTimer = null;
        }

        /// <summary>
        /// Attempts to transition from Open to HalfOpen state
        /// </summary>
        private void TryTransitionToHalfOpen()
        {
            lock (_stateLock)
            {
                if (_state == CircuitState.Open)
                {
                    ChangeState(CircuitState.HalfOpen);
                    _logger.LogInformation($"Circuit '{_name}' timeout elapsed, transitioning to half-open state");
                }
            }
        }
    }

    /// <summary>
    /// Event arguments for circuit state changes
    /// </summary>
    public class CircuitStateChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the circuit name
        /// </summary>
        public string CircuitName { get; }
        
        /// <summary>
        /// Gets the previous state
        /// </summary>
        public CircuitState PreviousState { get; }
        
        /// <summary>
        /// Gets the new state
        /// </summary>
        public CircuitState NewState { get; }
        
        /// <summary>
        /// Gets when the state changed
        /// </summary>
        public DateTime Timestamp { get; }
        
        /// <summary>
        /// Gets the current failure count
        /// </summary>
        public int FailureCount { get; }

        /// <summary>
        /// Creates a new instance of CircuitStateChangedEventArgs
        /// </summary>
        public CircuitStateChangedEventArgs(
            string circuitName,
            CircuitState previousState,
            CircuitState newState,
            DateTime timestamp,
            int failureCount)
        {
            CircuitName = circuitName;
            PreviousState = previousState;
            NewState = newState;
            Timestamp = timestamp;
            FailureCount = failureCount;
        }
    }

    /// <summary>
    /// Exception thrown when a circuit breaker is open
    /// </summary>
    public class CircuitBreakerOpenException : Exception
    {
        /// <summary>
        /// Creates a new instance of CircuitBreakerOpenException
        /// </summary>
        /// <param name="message">Exception message</param>
        public CircuitBreakerOpenException(string message) : base(message) { }
        
        /// <summary>
        /// Creates a new instance of CircuitBreakerOpenException
        /// </summary>
        /// <param name="message">Exception message</param>
        /// <param name="innerException">Inner exception</param>
        public CircuitBreakerOpenException(string message, Exception innerException) 
            : base(message, innerException) { }
    }
}
