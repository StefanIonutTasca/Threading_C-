using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls;

namespace TransportTracker.UI.ViewModels
{
    public class BaseViewModel : INotifyPropertyChanged
    {
        private bool _isBusy;
        private string _title;
        private bool _isRefreshing;

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        public bool IsNotBusy => !IsBusy;

        public bool IsRefreshing
        {
            get => _isRefreshing;
            set => SetProperty(ref _isRefreshing, value);
        }

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        protected bool SetProperty<T>(ref T backingField, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(backingField, value))
                return false;

            backingField = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void SetProperty<T>(ref T backingField, T value, Action onChanged, [CallerMemberName] string propertyName = null)
        {
            if (SetProperty(ref backingField, value, propertyName))
                onChanged?.Invoke();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected Task InvokeOnMainThreadAsync(Func<Task> action)
        {
            return MainThread.InvokeOnMainThreadAsync(action);
        }

        protected void InvokeOnMainThread(Action action)
        {
            MainThread.BeginInvokeOnMainThread(action);
        }

        /// <summary>
        /// Creates a command that can be used to handle user interaction with UI elements
        /// </summary>
        protected ICommand CreateCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            return new Command(execute, canExecute);
        }

        /// <summary>
        /// Creates an asynchronous command that can be used to handle user interaction with UI elements
        /// </summary>
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
            }, canExecute);
        }
    }
}
