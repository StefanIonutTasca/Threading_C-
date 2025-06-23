using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ThreadingCS.ViewModels
{
    /// <summary>
    /// Base view model implementing INotifyPropertyChanged for all view models
    /// </summary>
    public class BaseViewModel : INotifyPropertyChanged
    {
        /// <summary>
        /// Event for property changed notifications
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Method to raise PropertyChanged event for a property
        /// </summary>
        /// <param name="propertyName">Name of the property that changed</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Helper method to set property value and raise PropertyChanged event if value changed
        /// </summary>
        /// <typeparam name="T">Type of the property</typeparam>
        /// <param name="storage">Reference to the backing field</param>
        /// <param name="value">New value to set</param>
        /// <param name="propertyName">Name of the property (automatically provided by compiler)</param>
        /// <returns>True if value was changed, false otherwise</returns>
        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (Object.Equals(storage, value))
                return false;

            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
