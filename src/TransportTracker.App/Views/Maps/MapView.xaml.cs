using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using Microsoft.Maui.Devices.Sensors;
using System.Collections.ObjectModel;
using TransportTracker.App.Services;
using TransportTracker.App.ViewModels;

namespace TransportTracker.App.Views.Maps
{
    public partial class MapView : ContentPage
    {
        // For demo purposes - starting location (city center)
        private const double DEFAULT_LATITUDE = 52.370216;
        private const double DEFAULT_LONGITUDE = 4.895168;
        private const double DEFAULT_ZOOM = 14;
        
        // Will be replaced with actual data from API
        private ObservableCollection<TransportPin> _transportPins = new ObservableCollection<TransportPin>();
        private readonly MapViewModel _viewModel;

        public MapView()
        {
            InitializeComponent();
            
            // Set up the view model
            _viewModel = new MapViewModel();
            BindingContext = _viewModel;
            
            InitializeMap();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            
            // Request location permissions if needed
            var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            
            // Check if we have location permissions
            if (status == PermissionStatus.Granted)
            {
                TransportMap.IsShowingUser = true;
                await MoveToCurrentLocation();
            }
            else
            {
                // Move to default location if permission not granted
                MoveToDefaultLocation();
            }
            
            // Load initial vehicle data
            await LoadTransportVehicles();
        }

        private void InitializeMap()
        {
            // Set initial map position
            MoveToDefaultLocation();
            
            // Set up map pin collection
            _transportPins = new ObservableCollection<TransportPin>();
            
            // Register map pin clicked event
            TransportMap.MapClicked += OnMapClicked;
        }
        
        private async Task MoveToCurrentLocation()
        {
            try
            {
                var location = await Geolocation.GetLocationAsync(new GeolocationRequest
                {
                    DesiredAccuracy = GeolocationAccuracy.Medium,
                    Timeout = TimeSpan.FromSeconds(10)
                });

                if (location != null)
                {
                    await TransportMap.MoveToRegion(
                        MapSpan.FromCenterAndRadius(
                            new Location(location.Latitude, location.Longitude),
                            Distance.FromKilometers(1)));
                }
                else
                {
                    MoveToDefaultLocation();
                }
            }
            catch (Exception ex)
            {
                // Handle location errors (e.g., location services disabled)
                System.Diagnostics.Debug.WriteLine($"Location error: {ex.Message}");
                MoveToDefaultLocation();
            }
        }
        
        private void MoveToDefaultLocation()
        {
            TransportMap.MoveToRegion(
                MapSpan.FromCenterAndRadius(
                    new Location(DEFAULT_LATITUDE, DEFAULT_LONGITUDE),
                    Distance.FromKilometers(DEFAULT_ZOOM)));
        }
        
        private async Task LoadTransportVehicles()
        {
            try
            {
                // This will be replaced with real API data in the future
                var mockVehicles = GenerateMockVehicleData();
                
                // Clear existing pins
                TransportMap.Pins.Clear();
                
                // Add pins for each vehicle
                foreach (var vehicle in mockVehicles)
                {
                    AddTransportPin(vehicle);
                }
                
                // Notify the view model that data has been loaded
                _viewModel.IsDataLoaded = true;
                _viewModel.LastUpdated = DateTime.Now;
                _viewModel.VehicleCount = mockVehicles.Count;
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to load transport data: {ex.Message}", "OK");
            }
        }
        
        private List<TransportVehicle> GenerateMockVehicleData()
        {
            // Generate mock vehicle data for demonstration purposes
            // This will be replaced with real API data
            var random = new Random();
            var vehicles = new List<TransportVehicle>();
            
            // Define vehicle types and their corresponding colors
            var types = new[] { "Bus", "Train", "Tram", "Subway", "Ferry" };
            var colors = new[] { "#E63946", "#4361EE", "#06D6A0", "#FFD166", "#118AB2" };
            
            // Generate 10 random vehicles around the default location
            for (int i = 0; i < 10; i++)
            {
                // Generate location within ~2km of the default location
                var latOffset = (random.NextDouble() - 0.5) * 0.03;
                var lonOffset = (random.NextDouble() - 0.5) * 0.03;
                
                var typeIndex = random.Next(0, types.Length);
                var vehicle = new TransportVehicle
                {
                    Id = $"VEH-{i + 1}",
                    Type = types[typeIndex],
                    RouteNumber = $"{random.Next(1, 200)}",
                    CurrentSpeed = random.Next(0, 80),
                    Heading = random.Next(0, 360),
                    Latitude = DEFAULT_LATITUDE + latOffset,
                    Longitude = DEFAULT_LONGITUDE + lonOffset,
                    LastUpdated = DateTime.Now.AddSeconds(-random.Next(5, 300)),
                    Color = colors[typeIndex],
                    IsDelayed = random.Next(0, 5) == 0 // 20% chance of being delayed
                };
                vehicles.Add(vehicle);
            }
            
            return vehicles;
        }
        
        private void AddTransportPin(TransportVehicle vehicle)
        {
            // Create a new pin for the vehicle
            var pin = new Pin
            {
                Label = $"{vehicle.Type} {vehicle.RouteNumber}",
                Address = $"Speed: {vehicle.CurrentSpeed} km/h • Updated: {vehicle.LastUpdated:HH:mm:ss}",
                Location = new Location(vehicle.Latitude, vehicle.Longitude),
                Type = PinType.Generic
            };
            
            // Store the vehicle ID for reference when the pin is clicked
            pin.BindingContext = vehicle;
            
            // Add click handler
            pin.MarkerClicked += OnPinClicked;
            
            // Add to map
            TransportMap.Pins.Add(pin);
        }
        
        private async void OnPinClicked(object sender, PinClickedEventArgs e)
        {
            if (sender is Pin pin && pin.BindingContext is TransportVehicle vehicle)
            {
                // Show vehicle details when pin is clicked
                await DisplayAlert(
                    $"{vehicle.Type} {vehicle.RouteNumber}", 
                    $"ID: {vehicle.Id}\n" +
                    $"Speed: {vehicle.CurrentSpeed} km/h\n" +
                    $"Heading: {vehicle.Heading}°\n" +
                    $"Updated: {vehicle.LastUpdated:HH:mm:ss}\n" +
                    $"Status: {(vehicle.IsDelayed ? "Delayed" : "On time")}", 
                    "Close");
            }
            
            // Keep the pin selected
            e.HideInfoWindow = true;
        }
        
        private void OnMapClicked(object sender, MapClickedEventArgs e)
        {
            // Handle map clicks if needed
            System.Diagnostics.Debug.WriteLine($"Map clicked at {e.Location.Latitude}, {e.Location.Longitude}");
        }

        private async void OnMyLocationClicked(object sender, EventArgs e)
        {
            await MoveToCurrentLocation();
        }

        private void OnMapTypeChanged(object sender, EventArgs e)
        {
            if (sender is Picker picker)
            {
                switch (picker.SelectedIndex)
                {
                    case 0:
                        TransportMap.MapType = MapType.Street;
                        break;
                    case 1:
                        TransportMap.MapType = MapType.Satellite;
                        break;
                    case 2:
                        TransportMap.MapType = MapType.Hybrid;
                        break;
                }
            }
        }

        private async void OnRefreshMapClicked(object sender, EventArgs e)
        {
            await LoadTransportVehicles();
        }

        private void OnTransportFilterChanged(object sender, CheckedChangedEventArgs e)
        {
            // Filter pins based on transport type
            // (Will be implemented with real data)
            
            if (sender is CheckBox checkBox)
            {
                string transportType = "";
                
                if (checkBox == BusFilter) transportType = "Bus";
                else if (checkBox == TrainFilter) transportType = "Train";
                else if (checkBox == TramFilter) transportType = "Tram";
                else if (checkBox == SubwayFilter) transportType = "Subway";
                else if (checkBox == FerryFilter) transportType = "Ferry";
                
                foreach (var pin in TransportMap.Pins)
                {
                    if (pin.BindingContext is TransportVehicle vehicle && vehicle.Type == transportType)
                    {
                        // When using custom rendered pins, we would toggle visibility here
                        // For now, we'll just remove/add them
                        pin.IsVisible = checkBox.IsChecked;
                    }
                }
            }
        }
    }
    
    // Basic vehicle model (will be replaced by core domain model)
    public class TransportVehicle
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public string RouteNumber { get; set; }
        public double CurrentSpeed { get; set; }
        public int Heading { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public DateTime LastUpdated { get; set; }
        public string Color { get; set; }
        public bool IsDelayed { get; set; }
    }
    
    // Custom pin class (will be used for custom rendering)
    public class TransportPin : Pin
    {
        public string VehicleType { get; set; }
        public string Color { get; set; }
    }
}
