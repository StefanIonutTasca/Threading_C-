using System;
using System.Threading.Tasks;
using System.Windows.Input;
using TransportTracker.App.Core.MVVM;
using TransportTracker.App.Services;

namespace TransportTracker.App.ViewModels
{
    /// <summary>
    /// Main view model that serves as a coordinator for the application.
    /// </summary>
    public class MainViewModel : BaseViewModel
    {
        private readonly INavigationService _navigationService;
        private bool _isDarkTheme;

        public MainViewModel(INavigationService navigationService)
        {
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
            
            Title = "Transport Tracker";
            
            // Initialize commands
            NavigateToMapCommand = CreateAsyncCommand(() => _navigationService.NavigateToAsync("MapPage"));
            NavigateToVehiclesCommand = CreateAsyncCommand(() => _navigationService.NavigateToAsync("VehiclesPage"));
            NavigateToSettingsCommand = CreateAsyncCommand(() => _navigationService.NavigateToAsync("SettingsPage"));
            ToggleThemeCommand = CreateCommand(ToggleTheme);
            RefreshDataCommand = CreateAsyncCommand(RefreshAppDataAsync);
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
        /// Gets the command to navigate to the map page.
        /// </summary>
        public ICommand NavigateToMapCommand { get; }
        
        /// <summary>
        /// Gets the command to navigate to the vehicles page.
        /// </summary>
        public ICommand NavigateToVehiclesCommand { get; }
        
        /// <summary>
        /// Gets the command to navigate to the settings page.
        /// </summary>
        public ICommand NavigateToSettingsCommand { get; }
        
        /// <summary>
        /// Gets the command to toggle between light and dark themes.
        /// </summary>
        public ICommand ToggleThemeCommand { get; }
        
        /// <summary>
        /// Gets the command to refresh application data.
        /// </summary>
        public ICommand RefreshDataCommand { get; }
        
        /// <summary>
        /// Initializes the view model.
        /// </summary>
        public override async Task InitializeAsync()
        {
            if (IsInitialized)
                return;

            // Load user preferences
            await Task.Delay(100); // Simulating preference loading
            
            IsInitialized = true;
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
        /// Refreshes all application data.
        /// </summary>
        private async Task RefreshAppDataAsync()
        {
            if (IsBusy)
                return;
                
            try
            {
                IsBusy = true;
                
                // In a real implementation, this would refresh all view models
                // that are currently in use
                
                await Task.Delay(500); // Simulate work
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
