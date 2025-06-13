using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using TransportTracker.App.Core.MVVM;
using TransportTracker.App.Services;
using TransportTracker.App.Views.Maps;

namespace TransportTracker.App.ViewModels
{
    public class VehiclesViewModel : BaseViewModel
    {
        private readonly INavigationService _navigationService;
        private ObservableRangeCollection<TransportVehicle> _vehicles;
        private TransportVehicle _selectedVehicle;
        private string _searchQuery;
        private bool _isFiltering;
        private bool _showBuses = true;
        private bool _showTrains = true;
        private bool _showTrams = true;
        private bool _showSubways = true;
        private bool _showFerries = true;
        private DateTime _lastUpdated;

        public VehiclesViewModel(INavigationService navigationService)
        {
            _navigationService = navigationService;
            Title = "Vehicles";
            Icon = "vehicle_icon.png";
            Vehicles = new ObservableRangeCollection<TransportVehicle>();

            RefreshCommand = CreateAsyncCommand(RefreshVehiclesAsync);
            SelectVehicleCommand = CreateAsyncCommand<TransportVehicle>(SelectVehicleAsync);
            FilterCommand = CreateCommand(ApplyFilters);
            SearchCommand = CreateCommand<string>(ExecuteSearch);
            ClearSearchCommand = CreateCommand(ClearSearch);
            ShowOnMapCommand = CreateAsyncCommand<TransportVehicle>(ShowVehicleOnMapAsync);
        }

        public ObservableRangeCollection<TransportVehicle> Vehicles
        {
            get => _vehicles;
            set => SetProperty(ref _vehicles, value);
        }

        public TransportVehicle SelectedVehicle
        {
            get => _selectedVehicle;
            set => SetProperty(ref _selectedVehicle, value);
        }

        public string SearchQuery
        {
            get => _searchQuery;
            set => SetProperty(ref _searchQuery, value, () => SearchCommand.Execute(value));
        }

        public bool IsFiltering
        {
            get => _isFiltering;
            set => SetProperty(ref _isFiltering, value);
        }

        public bool ShowBuses
        {
            get => _showBuses;
            set => SetProperty(ref _showBuses, value, () => FilterCommand.Execute(null));
        }

        public bool ShowTrains
        {
            get => _showTrains;
            set => SetProperty(ref _showTrains, value, () => FilterCommand.Execute(null));
        }

        public bool ShowTrams
        {
            get => _showTrams;
            set => SetProperty(ref _showTrams, value, () => FilterCommand.Execute(null));
        }

        public bool ShowSubways
        {
            get => _showSubways;
            set => SetProperty(ref _showSubways, value, () => FilterCommand.Execute(null));
        }

        public bool ShowFerries
        {
            get => _showFerries;
            set => SetProperty(ref _showFerries, value, () => FilterCommand.Execute(null));
        }

        public DateTime LastUpdated
        {
            get => _lastUpdated;
            set => SetProperty(ref _lastUpdated, value);
        }

        public int TotalVehicleCount => Vehicles?.Count ?? 0;

        public ICommand RefreshCommand { get; }
        public ICommand SelectVehicleCommand { get; }
        public ICommand FilterCommand { get; }
        public ICommand SearchCommand { get; }
        public ICommand ClearSearchCommand { get; }
        public ICommand ShowOnMapCommand { get; }

        public override async Task InitializeAsync()
        {
            if (IsInitialized)
                return;

            await RefreshVehiclesAsync();
            IsInitialized = true;
        }

        private async Task RefreshVehiclesAsync()
        {
            if (IsBusy)
                return;

            try
            {
                IsRefreshing = true;

                // For now, generate mock data
                // This will be replaced with actual API calls later
                var mockVehicles = GenerateMockVehicles();
                Vehicles.ReplaceRange(mockVehicles);
                
                LastUpdated = DateTime.Now;
                OnPropertyChanged(nameof(TotalVehicleCount));
            }
            catch (Exception ex)
            {
                // In a real app, we'd log this error
                Console.WriteLine($"Error refreshing vehicles: {ex}");
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

        private async Task SelectVehicleAsync(TransportVehicle vehicle)
        {
            if (vehicle == null)
                return;

            SelectedVehicle = null; // Reset selection
            
            // Navigate to vehicle details page
            // This will be implemented later when we have the page
            await Task.CompletedTask;
        }

        private void ApplyFilters()
        {
            // In a real app, we'd filter the actual data source
            // For now, we'll just simulate filtering by refreshing
            RefreshCommand.Execute(null);
        }

        private void ExecuteSearch(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                ClearSearch();
                return;
            }

            // In a real app, we'd filter based on the search query
            // For now, we'll just simulate searching by refreshing
            RefreshCommand.Execute(null);
        }

        private void ClearSearch()
        {
            SearchQuery = string.Empty;
            // In a real app, we'd reset the filter
            // For now, just refresh
            RefreshCommand.Execute(null);
        }

        private async Task ShowVehicleOnMapAsync(TransportVehicle vehicle)
        {
            if (vehicle == null)
                return;

            // Navigate to map view centered on this vehicle
            await _navigationService.NavigateToAsync("MapPage", new Dictionary<string, object>
            {
                { "vehicleId", vehicle.Id }
            });
        }
    }
}
