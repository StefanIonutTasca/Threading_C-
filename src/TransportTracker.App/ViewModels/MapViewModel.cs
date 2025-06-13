using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Maps;
using TransportTracker.App.Core.MVVM;
using TransportTracker.App.Views.Maps;

namespace TransportTracker.App.ViewModels
{
    public class MapViewModel : BaseViewModel
    {
        private bool _isDataLoaded;
        private DateTime _lastUpdated;
        private int _vehicleCount;
        private bool _isRefreshing;
        private string _selectedMapType = "Street";
        private Dictionary<string, bool> _transportFilters;

        public MapViewModel()
        {
            // Set view model properties
            Title = "Map";
            Icon = "map_icon.png";
            
            // Initialize commands
            RefreshCommand = CreateAsyncCommand(RefreshData);
            ChangeMapTypeCommand = CreateCommand<string>(OnMapTypeChanged);
            ToggleFilterCommand = CreateCommand<string>(OnFilterToggled);
            ZoomToUserLocationCommand = CreateAsyncCommand(ZoomToUserLocationAsync);
            
            // Initialize filters for transport types
            _transportFilters = new Dictionary<string, bool>
            {
                { "Bus", true },
                { "Train", true },
                { "Tram", true },
                { "Subway", true },
                { "Ferry", true }
            };
            
            // Initialize map pins collection
            VehiclePins = new ObservableRangeCollection<Pin>();
        }

        public bool IsDataLoaded
        {
            get => _isDataLoaded;
            set => SetProperty(ref _isDataLoaded, value);
        }

        public DateTime LastUpdated
        {
            get => _lastUpdated;
            set => SetProperty(ref _lastUpdated, value, () => OnPropertyChanged(nameof(LastUpdatedText)));
        }

        public string LastUpdatedText => LastUpdated != DateTime.MinValue 
            ? $"Last Updated: {LastUpdated:HH:mm:ss}" 
            : "Not updated yet";

        public int VehicleCount
        {
            get => _vehicleCount;
            set => SetProperty(ref _vehicleCount, value, () => OnPropertyChanged(nameof(VehicleCountText)));
        }

        public string VehicleCountText => $"{VehicleCount} vehicles visible";

        public string SelectedMapType
        {
            get => _selectedMapType;
            set => SetProperty(ref _selectedMapType, value);
        }

        public Dictionary<string, bool> TransportFilters
        {
            get => _transportFilters;
            set => SetProperty(ref _transportFilters, value);
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
        public ICommand ChangeMapTypeCommand { get; }
        public ICommand ToggleFilterCommand { get; }
        public ICommand ZoomToUserLocationCommand { get; }
        
        public ObservableRangeCollection<Pin> VehiclePins { get; private set; }
        
        public Location UserLocation { get; private set; }

        public override async Task InitializeAsync()
        {
            if (IsInitialized)
                return;
                
            await RefreshData();
            IsInitialized = true;
        }
        
        private async Task RefreshData()
        {
            if (IsBusy)
                return;

            try
            {
                IsRefreshing = true;
                
                // Simulating a refresh operation
                await Task.Delay(1000);
                
                // Generate mock vehicle data
                var mockVehicles = GenerateMockVehicles();
                UpdateMapPins(mockVehicles);
                
                // This will be replaced with actual API call in the future
                LastUpdated = DateTime.Now;
                VehicleCount = VehiclePins.Count;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error refreshing data: {ex.Message}");
            }
            finally
            {
                IsRefreshing = false;
            }
        }
        
        private List<TransportVehicle> GenerateMockVehicles()
        {
            var random = new Random();
            var vehicles = new List<TransportVehicle>();
            
            var types = new[] { "Bus", "Train", "Tram", "Subway", "Ferry" };
            var statuses = new[] { "On Time", "Delayed", "Out of Service", "Arriving", "Departing" };
            
            for (int i = 1; i <= 50; i++)
            {
                var type = types[random.Next(types.Length)];
                
                // Only add vehicles of types that are not filtered out
                if (!GetFilter(type))
                    continue;
                    
                vehicles.Add(new TransportVehicle
                {
                    Id = Guid.NewGuid().ToString(),
                    Number = $"{type[0]}-{random.Next(100, 999)}",
                    Type = type,
                    Route = $"Route {random.Next(1, 30)}",
                    NextStop = $"Stop {random.Next(1, 20)}",
                    Status = statuses[random.Next(statuses.Length)],
                    Capacity = random.Next(10, 250),
                    Occupancy = random.Next(1, 250),
                    LastUpdated = DateTime.Now.AddMinutes(-random.Next(1, 30)),
                    Latitude = 51.5 + (random.NextDouble() - 0.5) * 0.1,
                    Longitude = -0.12 + (random.NextDouble() - 0.5) * 0.1,
                    Speed = random.Next(0, 120)
                });
            }
            
            return vehicles;
        }
        
        private void UpdateMapPins(List<TransportVehicle> vehicles)
        {
            // Clear existing pins
            VehiclePins.Clear();
            
            // Add a pin for each vehicle
            foreach (var vehicle in vehicles)
            {
                if (TransportFilters.TryGetValue(vehicle.Type, out bool isVisible) && isVisible)
                {
                    var pin = new Pin
                    {
                        Label = vehicle.Number,
                        Address = $"{vehicle.Route} - {vehicle.Status}",
                        Location = new Location(vehicle.Latitude, vehicle.Longitude),
                        Type = PinType.Place,
                        BindingContext = vehicle
                    };
                    
                    VehiclePins.Add(pin);
                }
            }
        }
        
        private void OnMapTypeChanged(string mapType)
        {
            if (!string.IsNullOrEmpty(mapType))
            {
                SelectedMapType = mapType;
            }
        }
        
        private void OnFilterToggled(string transportType)
        {
            if (string.IsNullOrEmpty(transportType))
                return;
                
            // Toggle filter status
            SetFilter(transportType, !GetFilter(transportType));
            
            // Refresh data with the new filter
            RefreshCommand.Execute(null);
        }
        
        private async Task ZoomToUserLocationAsync()
        {
            try
            {
                var request = new GeolocationRequest(GeolocationAccuracy.Medium);
                var location = await Geolocation.GetLocationAsync(request);
                
                if (location != null)
                {
                    UserLocation = new Location(location.Latitude, location.Longitude);
                    OnPropertyChanged(nameof(UserLocation));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Unable to get location: {ex.Message}");
                // In a real app, we'd show a user-friendly error message
            }
        }
    }
}
