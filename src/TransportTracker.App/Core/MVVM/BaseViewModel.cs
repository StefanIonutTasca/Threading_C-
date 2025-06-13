using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls;

namespace TransportTracker.App.Core.MVVM
{
    /// <summary>
    /// Base implementation for all ViewModel classes in the application.
    /// Provides common functionality and property change notification support.
    /// </summary>
    public abstract class BaseViewModel : INotifyPropertyChanged, IDisposable
    {
        private bool _isBusy;
        private bool _isRefreshing;
        private string _title;
        private string _icon;
        private bool _isInitialized;
        private bool _disposedValue;

        /// <summary>
        /// Gets or sets a value indicating whether this instance is busy with a background operation.
        /// </summary>
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    OnPropertyChanged(nameof(IsNotBusy));
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether this instance is not busy.
        /// </summary>
        public bool IsNotBusy => !IsBusy;

        /// <summary>
        /// Gets or sets a value indicating whether this instance is refreshing data.
        /// </summary>
        public bool IsRefreshing
        {
            get => _isRefreshing;
            set => SetProperty(ref _isRefreshing, value);
        }

        /// <summary>
        /// Gets or sets the title of the view model.
        /// </summary>
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        /// <summary>
        /// Gets or sets the icon for the view model (used in navigation).
        /// </summary>
        public string Icon
        {
            get => _icon;
            set => SetProperty(ref _icon, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether this instance has been initialized.
        /// </summary>
        public bool IsInitialized
        {
            get => _isInitialized;
            protected set => SetProperty(ref _isInitialized, value);
        }

        #region INotifyPropertyChanged Implementation

        /// <summary>
        /// Occurs when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Raises the PropertyChanged event for the specified property name.
        /// </summary>
        /// <param name="propertyName">Name of the property that changed.</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Sets the property value and raises the PropertyChanged event if the value has changed.
        /// </summary>
        /// <typeparam name="T">Type of the property.</typeparam>
        /// <param name="backingField">Reference to the backing field for the property.</param>
        /// <param name="value">New value for the property.</param>
        /// <param name="propertyName">Name of the property. Automatically populated by the compiler.</param>
        /// <returns>True if the property was changed; otherwise, false.</returns>
        protected bool SetProperty<T>(ref T backingField, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(backingField, value))
                return false;

            backingField = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        /// <summary>
        /// Sets the property value, raises the PropertyChanged event if the value has changed, and executes a callback.
        /// </summary>
        /// <typeparam name="T">Type of the property.</typeparam>
        /// <param name="backingField">Reference to the backing field for the property.</param>
        /// <param name="value">New value for the property.</param>
        /// <param name="onChanged">Action to execute if the value changes.</param>
        /// <param name="propertyName">Name of the property. Automatically populated by the compiler.</param>
        /// <returns>True if the property was changed; otherwise, false.</returns>
        protected bool SetProperty<T>(ref T backingField, T value, Action onChanged, [CallerMemberName] string propertyName = null)
        {
            if (SetProperty(ref backingField, value, propertyName))
            {
                onChanged?.Invoke();
                return true;
            }
            
            return false;
        }

        #endregion

        #region Commands

        /// <summary>
        /// Creates a command that can be used to handle user interaction with UI elements.
        /// </summary>
        /// <param name="execute">Action to execute when the command is invoked.</param>
        /// <param name="canExecute">Function to determine if the command can be executed.</param>
        /// <returns>A command that can be bound to the UI.</returns>
        protected ICommand CreateCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            return new Command(execute, canExecute);
        }

        /// <summary>
        /// Creates a command that can be used to handle user interaction with UI elements.
        /// </summary>
        /// <param name="execute">Action to execute when the command is invoked.</param>
        /// <param name="canExecute">Function to determine if the command can be executed.</param>
        /// <returns>A command that can be bound to the UI.</returns>
        protected ICommand CreateCommand(Action execute, Func<bool> canExecute = null)
        {
            return new Command(execute, canExecute != null ? () => canExecute() : null);
        }

        /// <summary>
        /// Creates an asynchronous command that can be used to handle user interaction with UI elements.
        /// </summary>
        /// <param name="execute">Asynchronous action to execute when the command is invoked.</param>
        /// <param name="canExecute">Function to determine if the command can be executed.</param>
        /// <returns>A command that can be bound to the UI.</returns>
        protected ICommand CreateAsyncCommand(Func<Task> execute, Func<bool> canExecute = null)
        {
            return new Command(async () =>
            {
                IsBusy = true;
                try
                {
                    await execute();
                }
                finally
                {
                    IsBusy = false;
                }
            }, canExecute != null ? () => !IsBusy && canExecute() : () => !IsBusy);
        }

        /// <summary>
        /// Creates an asynchronous command that can be used to handle user interaction with UI elements.
        /// </summary>
        /// <param name="execute">Asynchronous action to execute when the command is invoked.</param>
        /// <param name="canExecute">Function to determine if the command can be executed.</param>
        /// <returns>A command that can be bound to the UI.</returns>
        protected ICommand CreateAsyncCommand(Func<object, Task> execute, Func<object, bool> canExecute = null)
        {
            return new Command(async param =>
            {
                IsBusy = true;
                try
                {
                    await execute(param);
                }
                finally
                {
                    IsBusy = false;
                }
            }, canExecute != null ? param => !IsBusy && canExecute(param) : _ => !IsBusy);
        }

        #endregion

        #region Threading Helpers

        /// <summary>
        /// Invokes an asynchronous action on the main UI thread.
        /// </summary>
        /// <param name="action">Asynchronous action to execute.</param>
        /// <returns>Task representing the asynchronous operation.</returns>
        protected Task InvokeOnMainThreadAsync(Func<Task> action)
        {
            return MainThread.InvokeOnMainThreadAsync(action);
        }

        /// <summary>
        /// Invokes an action on the main UI thread.
        /// </summary>
        /// <param name="action">Action to execute.</param>
        protected void InvokeOnMainThread(Action action)
        {
            MainThread.BeginInvokeOnMainThread(action);
        }

        #endregion

        #region Lifecycle Methods

        /// <summary>
        /// Initializes the view model. Override this method to perform initialization tasks.
        /// </summary>
        /// <returns>Task representing the asynchronous operation.</returns>
        public virtual Task InitializeAsync()
        {
            IsInitialized = true;
            return Task.CompletedTask;
        }
        
        /// <summary>
        /// Called when the view appears. Override this method to handle view appearing events.
        /// </summary>
        /// <returns>Task representing the asynchronous operation.</returns>
        public virtual Task OnAppearingAsync()
        {
            return Task.CompletedTask;
        }
        
        /// <summary>
        /// Called when the view disappears. Override this method to handle view disappearing events.
        /// </summary>
        /// <returns>Task representing the asynchronous operation.</returns>
        public virtual Task OnDisappearingAsync()
        {
            return Task.CompletedTask;
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Disposes the view model resources.
        /// </summary>
        /// <param name="disposing">True if disposing managed resources; otherwise, false.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    // Dispose managed resources
                }
                
                _disposedValue = true;
            }
        }

        /// <summary>
        /// Disposes the view model resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
