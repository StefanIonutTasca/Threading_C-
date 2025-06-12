using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace TransportTracker.Core.Threading.Events
{
    /// <summary>
    /// Thread-safe implementation of the event aggregator pattern.
    /// Provides a central hub for publishing messages across different threads
    /// while ensuring thread-safety and preventing UI thread blocking.
    /// </summary>
    public class ThreadSafeEventAggregator : IDisposable
    {
        private readonly ILogger<ThreadSafeEventAggregator> _logger;
        private readonly ConcurrentDictionary<Type, object> _subscriptions;
        private readonly SynchronizationContext _mainContext;
        private bool _disposed;
        
        /// <summary>
        /// Creates a new thread-safe event aggregator
        /// </summary>
        /// <param name="logger">Logger instance</param>
        public ThreadSafeEventAggregator(ILogger<ThreadSafeEventAggregator> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _subscriptions = new ConcurrentDictionary<Type, object>();
            
            // Capture the current synchronization context (should be UI thread context when created)
            _mainContext = SynchronizationContext.Current ?? new SynchronizationContext();
        }
        
        /// <summary>
        /// Publishes a message to all subscribers
        /// </summary>
        /// <typeparam name="TMessage">The type of the message</typeparam>
        /// <param name="message">The message to publish</param>
        /// <param name="runOnMainThread">Whether subscribers should be notified on the main/UI thread</param>
        public void Publish<TMessage>(TMessage message, bool runOnMainThread = false)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));
                
            if (_disposed)
                throw new ObjectDisposedException(nameof(ThreadSafeEventAggregator));
                
            var messageType = typeof(TMessage);
            
            if (_subscriptions.TryGetValue(messageType, out var subscriptionObj) && 
                subscriptionObj is ConcurrentBag<Action<TMessage>> subscriptions)
            {
                foreach (var subscription in subscriptions)
                {
                    try
                    {
                        if (runOnMainThread)
                        {
                            // Execute on the main thread (captured context)
                            _mainContext.Post(_ => subscription(message), null);
                        }
                        else
                        {
                            // Execute on the current thread
                            subscription(message);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error publishing {messageType.Name} message to subscriber");
                    }
                }
            }
            else
            {
                _logger.LogDebug($"No subscribers for message type: {messageType.Name}");
            }
        }
        
        /// <summary>
        /// Publishes a message asynchronously to all subscribers
        /// </summary>
        /// <typeparam name="TMessage">The type of the message</typeparam>
        /// <param name="message">The message to publish</param>
        /// <param name="runOnMainThread">Whether subscribers should be notified on the main/UI thread</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task PublishAsync<TMessage>(TMessage message, bool runOnMainThread = false, CancellationToken cancellationToken = default)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));
                
            if (_disposed)
                throw new ObjectDisposedException(nameof(ThreadSafeEventAggregator));
                
            var messageType = typeof(TMessage);
            
            if (_subscriptions.TryGetValue(messageType, out var subscriptionObj) && 
                subscriptionObj is ConcurrentBag<Action<TMessage>> subscriptions)
            {
                var tasks = new List<Task>();
                
                foreach (var subscription in subscriptions)
                {
                    var task = Task.Run(() => 
                    {
                        try
                        {
                            if (runOnMainThread)
                            {
                                var tcs = new TaskCompletionSource<bool>();
                                
                                _mainContext.Post(_ => 
                                {
                                    try
                                    {
                                        subscription(message);
                                        tcs.SetResult(true);
                                    }
                                    catch (Exception ex)
                                    {
                                        tcs.SetException(ex);
                                    }
                                }, null);
                                
                                return tcs.Task;
                            }
                            else
                            {
                                subscription(message);
                                return Task.CompletedTask;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Error publishing {messageType.Name} message to subscriber");
                            return Task.CompletedTask;
                        }
                    }, cancellationToken);
                    
                    tasks.Add(task);
                }
                
                await Task.WhenAll(tasks);
            }
            else
            {
                _logger.LogDebug($"No subscribers for message type: {messageType.Name}");
            }
        }
        
        /// <summary>
        /// Subscribes to messages of a specific type
        /// </summary>
        /// <typeparam name="TMessage">The type of message to subscribe to</typeparam>
        /// <param name="action">The action to execute when a message is received</param>
        /// <returns>A subscription token that can be used to unsubscribe</returns>
        public SubscriptionToken Subscribe<TMessage>(Action<TMessage> action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));
                
            if (_disposed)
                throw new ObjectDisposedException(nameof(ThreadSafeEventAggregator));
                
            var messageType = typeof(TMessage);
            
            var subscriptions = _subscriptions.GetOrAdd(
                messageType, 
                _ => new ConcurrentBag<Action<TMessage>>()) as ConcurrentBag<Action<TMessage>>;
            
            subscriptions.Add(action);
            
            _logger.LogDebug($"Added subscription for {messageType.Name}");
            
            return new SubscriptionToken(this, messageType, action);
        }
        
        /// <summary>
        /// Unsubscribes from messages of a specific type
        /// </summary>
        /// <typeparam name="TMessage">The type of message</typeparam>
        /// <param name="action">The action to unsubscribe</param>
        /// <returns>True if unsubscribed successfully</returns>
        public bool Unsubscribe<TMessage>(Action<TMessage> action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));
                
            if (_disposed)
                return false;
                
            var messageType = typeof(TMessage);
            
            if (_subscriptions.TryGetValue(messageType, out var subscriptionObj) && 
                subscriptionObj is ConcurrentBag<Action<TMessage>> subscriptions)
            {
                // Since ConcurrentBag doesn't support removal, we need to recreate it
                var newBag = new ConcurrentBag<Action<TMessage>>();
                bool removed = false;
                
                foreach (var subscription in subscriptions)
                {
                    if (!subscription.Equals(action))
                    {
                        newBag.Add(subscription);
                    }
                    else
                    {
                        removed = true;
                    }
                }
                
                // Replace the old bag with the new one without the removed subscription
                _subscriptions.TryUpdate(messageType, newBag, subscriptions);
                
                if (removed)
                {
                    _logger.LogDebug($"Removed subscription for {messageType.Name}");
                }
                
                return removed;
            }
            
            return false;
        }
        
        /// <summary>
        /// Disposes the event aggregator and clears all subscriptions
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;
                
            _subscriptions.Clear();
            _disposed = true;
            
            GC.SuppressFinalize(this);
        }
        
        /// <summary>
        /// Represents a token for managing message subscriptions
        /// </summary>
        public class SubscriptionToken : IDisposable
        {
            private readonly ThreadSafeEventAggregator _aggregator;
            private readonly Type _messageType;
            private readonly object _action;
            private bool _disposed;
            
            internal SubscriptionToken(ThreadSafeEventAggregator aggregator, Type messageType, object action)
            {
                _aggregator = aggregator;
                _messageType = messageType;
                _action = action;
            }
            
            /// <summary>
            /// Disposes the token and unsubscribes from the event
            /// </summary>
            public void Dispose()
            {
                if (_disposed)
                    return;
                    
                _disposed = true;
                
                // Use reflection to call the appropriate Unsubscribe method
                var unsubscribeMethod = _aggregator.GetType().GetMethod("Unsubscribe");
                var genericMethod = unsubscribeMethod.MakeGenericMethod(_messageType);
                genericMethod.Invoke(_aggregator, new[] { _action });
                
                GC.SuppressFinalize(this);
            }
        }
    }
}
