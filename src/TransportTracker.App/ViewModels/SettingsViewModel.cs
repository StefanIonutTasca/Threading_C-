using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using TransportTracker.App.Core.MVVM;
using TransportTracker.App.Services;

namespace TransportTracker.App.ViewModels
{
    /// <summary>
    /// View model for the settings page that handles user preferences.
    /// </summary>
    public class SettingsViewModel : BaseViewModel
    {
        private readonly INavigationService _navigationService;
        private bool _isDarkTheme;
        private string _selectedMapType = "Street";
        private bool _useRealTimeLocation;
        private bool _enableNotifications;
        private string _dataRefreshFrequency = "30 seconds";

        /// <summary>
        /// Initializes a new instance of the <see cref="SettingsViewModel"/> class.
        /// </summary>
        /// <param name="navigationService">The navigation service.</param>
        public SettingsViewModel(INavigationService navigationService)
        {
            _navigationService = navigationService;
            
            Title = "Settings";
            Icon = "settings_icon.png";
            
            // Initialize commands
            SaveSettingsCommand = CreateAsyncCommand(SaveSettingsAsync);
            ResetSettingsCommand = CreateCommand(ResetSettings);
            ToggleThemeCommand = CreateCommand(ToggleTheme);
            SelectMapTypeCommand = CreateCommand((object param) =>
{
    if (param is string str)
        SelectMapType(str);
});
        }

        /// <summary>
        /// Gets or sets a value indicating whether the app is using dark theme.
        /// </summary>
        public bool IsDarkTheme
        {
            get => _isDarkTheme;
            set => SetProperty(ref _isDarkTheme, value);
        }

        /// <summary>
        /// Gets or sets the selected map type.
        /// </summary>
        public string SelectedMapType
        {
            get => _selectedMapType;
            set => SetProperty(ref _selectedMapType, value);
        }

        /// <summary>
        /// Gets the available map types.
        /// </summary>
        public List<string> AvailableMapTypes { get; } = new List<string>
        {
            "Street",
            "Satellite",
            "Hybrid"
        };

        /// <summary>
        /// Gets or sets a value indicating whether real-time location is used.
        /// </summary>
        public bool UseRealTimeLocation
        {
            get => _useRealTimeLocation;
            set => SetProperty(ref _useRealTimeLocation, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether notifications are enabled.
        /// </summary>
        public bool EnableNotifications
        {
            get => _enableNotifications;
            set => SetProperty(ref _enableNotifications, value);
        }

        /// <summary>
        /// Gets or sets the data refresh frequency.
        /// </summary>
        public string DataRefreshFrequency
        {
            get => _dataRefreshFrequency;
            set => SetProperty(ref _dataRefreshFrequency, value);
        }

        /// <summary>
        /// Gets the available data refresh frequencies.
        /// </summary>
        public List<string> AvailableRefreshFrequencies { get; } = new List<string>
        {
            "10 seconds",
            "30 seconds",
            "1 minute",
            "5 minutes"
        };

        /// <summary>
        /// Gets the command to save settings.
        /// </summary>
        public ICommand SaveSettingsCommand { get; }

        /// <summary>
        /// Gets the command to reset settings to default values.
        /// </summary>
        public ICommand ResetSettingsCommand { get; }

        /// <summary>
        /// Gets the command to toggle between light and dark themes.
        /// </summary>
        public ICommand ToggleThemeCommand { get; }

        /// <summary>
        /// Gets the command to select a map type.
        /// </summary>
        public ICommand SelectMapTypeCommand { get; }

        /// <summary>
        /// Initializes the view model.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public override async Task InitializeAsync()
        {
            if (IsInitialized)
                return;

            // Load settings from preferences or default values
            await LoadSettingsAsync();

            IsInitialized = true;
        }

        /// <summary>
        /// Loads settings from preferences.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private async Task LoadSettingsAsync()
        {
            try
            {
                // In a real implementation, this would load from preferences
                // For now, we'll just set some defaults
                IsDarkTheme = false;
                SelectedMapType = "Street";
                UseRealTimeLocation = true;
                EnableNotifications = true;
                DataRefreshFrequency = "30 seconds";
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Saves the current settings to preferences.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private async Task SaveSettingsAsync()
        {
            if (IsBusy)
                return;

            try
            {
                IsBusy = true;

                // In a real implementation, this would save to preferences
                // For now, we'll just simulate a delay
                await Task.Delay(500);
                
                // Navigate back after saving
                await _navigationService.GoBackAsync();
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Resets settings to default values.
        /// </summary>
        private void ResetSettings()
        {
            IsDarkTheme = false;
            SelectedMapType = "Street";
            UseRealTimeLocation = true;
            EnableNotifications = true;
            DataRefreshFrequency = "30 seconds";
        }

        /// <summary>
        /// Toggles between light and dark themes.
        /// </summary>
        private void ToggleTheme()
        {
            IsDarkTheme = !IsDarkTheme;
            
            // In a real implementation, this would update app resources
            // Application.Current.Resources.ApplyDarkTheme(IsDarkTheme);
        }

        /// <summary>
        /// Selects a map type.
        /// </summary>
        /// <param name="mapType">The map type to select.</param>
        private void SelectMapType(string mapType)
        {
            if (!string.IsNullOrEmpty(mapType) && AvailableMapTypes.Contains(mapType))
            {
                SelectedMapType = mapType;
            }
        }
    }
}
