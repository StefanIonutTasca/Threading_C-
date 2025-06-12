using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace TransportTracker.App.ViewModels
{
    public class MapViewModel : INotifyPropertyChanged
    {
        private bool _isDataLoaded;
        private DateTime _lastUpdated;
        private int _vehicleCount;
        private bool _isRefreshing;
        private string _selectedMapType = "Street";
        private Dictionary<string, bool> _transportFilters;

        public MapViewModel()
        {
            // Initialize commands
            RefreshCommand = new Command(async () => await RefreshData());
            
            // Initialize filters for transport types
            _transportFilters = new Dictionary<string, bool>
            {
                { "Bus", true },
                { "Train", true },
                { "Tram", true },
                { "Subway", true },
                { "Ferry", true }
            };
        }

        public bool IsDataLoaded
        {
            get => _isDataLoaded;
            set
            {
                if (_isDataLoaded != value)
                {
                    _isDataLoaded = value;
                    OnPropertyChanged();
                }
            }
        }

        public DateTime LastUpdated
        {
            get => _lastUpdated;
            set
            {
                if (_lastUpdated != value)
                {
                    _lastUpdated = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(LastUpdatedText));
                }
            }
        }

        public string LastUpdatedText => LastUpdated != DateTime.MinValue 
            ? $"Last Updated: {LastUpdated:HH:mm:ss}" 
            : "Not updated yet";

        public int VehicleCount
        {
            get => _vehicleCount;
            set
            {
                if (_vehicleCount != value)
                {
                    _vehicleCount = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(VehicleCountText));
                }
            }
        }

        public string VehicleCountText => $"{VehicleCount} vehicles visible";

        public bool IsRefreshing
        {
            get => _isRefreshing;
            set
            {
                if (_isRefreshing != value)
                {
                    _isRefreshing = value;
                    OnPropertyChanged();
                }
            }
        }

        public string SelectedMapType
        {
            get => _selectedMapType;
            set
            {
                if (_selectedMapType != value)
                {
                    _selectedMapType = value;
                    OnPropertyChanged();
                }
            }
        }

        public Dictionary<string, bool> TransportFilters
        {
            get => _transportFilters;
            set
            {
                if (_transportFilters != value)
                {
                    _transportFilters = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool GetFilter(string transportType)
        {
            if (_transportFilters.TryGetValue(transportType, out bool value))
            {
                return value;
            }
            return true; // Default to showing all types
        }

        public void SetFilter(string transportType, bool value)
        {
            if (_transportFilters.ContainsKey(transportType))
            {
                _transportFilters[transportType] = value;
                OnPropertyChanged(nameof(TransportFilters));
            }
        }

        public ICommand RefreshCommand { get; }

        private async Task RefreshData()
        {
            if (IsRefreshing)
                return;

            try
            {
                IsRefreshing = true;
                
                // Simulating a refresh operation
                await Task.Delay(1000);
                
                // This will be replaced with actual API call in the future
                LastUpdated = DateTime.Now;
                
                // Update is complete
                IsRefreshing = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error refreshing data: {ex.Message}");
                IsRefreshing = false;
            }
        }

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
