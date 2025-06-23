using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using TransportTracker.App.Services;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using TransportTracker.App.Core.MVVM;
using TransportTracker.Core.Services;
using TransportTracker.App.Views.Maps;
using TransportTracker.App.Views.Vehicles;

namespace TransportTracker.App.ViewModels
{
    public class VehiclesViewModel : BaseViewModel
    {
        private readonly INavigationService _navigationService;
        private ObservableRangeCollection<TransportVehicle> _vehicles;
        private ObservableRangeCollection<TransportVehicle> _filteredVehicles;
        private ObservableCollection<Grouping<string, TransportVehicle>> _groupedVehicles;
        private TransportVehicle _selectedVehicle;
        private string _searchText;
        private bool _areFiltersVisible;
        private bool _isLoadingMore;

        private Dictionary<string, bool> _transportFilters;
        private Dictionary<string, bool> _statusFilters;

        private string _selectedSortOption;
        private int _currentPage = 1;
        private int _pageSize = 20;
        private DateTime _lastUpdated;

        public VehiclesViewModel(INavigationService navigationService)
        {
            _navigationService = navigationService;
            Title = "Vehicles";
            Icon = "vehicle_icon.png";
            
            // Initialize collections
            Vehicles = new ObservableRangeCollection<TransportVehicle>();
            FilteredVehicles = new ObservableRangeCollection<TransportVehicle>();
            GroupedVehicles = new ObservableCollection<Grouping<string, TransportVehicle>>();
            
            // Initialize filters
            _transportFilters = new Dictionary<string, bool>
            {
                { "Bus", true },
                { "Train", true },
                { "Tram", true },
                { "Subway", true },
                { "Ferry", true }
            };
            
            _statusFilters = new Dictionary<string, bool>
            {
                { "On Time", true },
                { "Delayed", true },
                { "Arriving", true },
                { "Departing", true },
                { "Out of Service", true }
            };
            
            // Initialize sort options
            SortOptions = new ObservableCollection<string>
            {
                "Type",
                "Route",
                "Status",
                "Last Updated"
            };
            _selectedSortOption = SortOptions[0];
            
            // Create commands
            RefreshCommand = CreateAsyncCommand(async () => await RefreshVehiclesAsync());
            VehicleSelectedCommand = CreateAsyncCommand(async (object param) => await SelectVehicleAsync(param));
            ToggleFiltersCommand = CreateCommand(ToggleFilters);
            ToggleFilterCommand = CreateCommand((object param) =>
{
    if (param is string str)
        ToggleTransportFilter(str);
});
            ToggleStatusFilterCommand = CreateCommand((object param) =>
{
    if (param is string str)
        ToggleStatusFilter(str);
});
            SearchCommand = CreateCommand((object param) =>
{
    if (param is string str)
        ExecuteSearch(str);
});
            ClearSearchCommand = CreateCommand(ClearSearch);
            ShowOnMapCommand = CreateAsyncCommand(async (object param) =>
{
    if (param is TransportVehicle vehicle)
        await ShowVehicleOnMapAsync(vehicle);
});
            ApplySortCommand = CreateCommand(ApplySort);
            LoadMoreItemsCommand = CreateAsyncCommand(LoadMoreItemsAsync);
        }

        public ObservableRangeCollection<TransportVehicle> Vehicles
        {
            get => _vehicles;
            set => SetProperty(ref _vehicles, value);
        }
        
        public ObservableRangeCollection<TransportVehicle> FilteredVehicles
        {
            get => _filteredVehicles;
            set => SetProperty(ref _filteredVehicles, value);
        }
        
        public ObservableCollection<Grouping<string, TransportVehicle>> GroupedVehicles
        {
            get => _groupedVehicles;
            set => SetProperty(ref _groupedVehicles, value);
        }

        public TransportVehicle SelectedVehicle
        {
            get => _selectedVehicle;
            set => SetProperty(ref _selectedVehicle, value);
        }

        public string SearchText
        {
            get => _searchText;
            set => SetProperty(ref _searchText, value);
        }
        
        public bool AreFiltersVisible
        {
            get => _areFiltersVisible;
            set => SetProperty(ref _areFiltersVisible, value);
        }
        
        public bool IsLoadingMore
        {
            get => _isLoadingMore;
            set => SetProperty(ref _isLoadingMore, value);
        }
        
        public ObservableCollection<string> SortOptions { get; private set; }
        
        public string SortOption
        {
            get => _selectedSortOption;
            set => SetProperty(ref _selectedSortOption, value, ApplySort);
        }

        public DateTime LastUpdated
        {
            get => _lastUpdated;
            set => SetProperty(ref _lastUpdated, value);
        }

        public int TotalVehicleCount => Vehicles?.Count ?? 0;
        public int FilteredVehicleCount => FilteredVehicles?.Count ?? 0;

        public ICommand RefreshCommand { get; }
        public ICommand VehicleSelectedCommand { get; }
        public ICommand ToggleFiltersCommand { get; }
        public ICommand ToggleFilterCommand { get; }
        public ICommand ToggleStatusFilterCommand { get; }
        public ICommand SearchCommand { get; }
        public ICommand ClearSearchCommand { get; }
        public ICommand ShowOnMapCommand { get; }
        public ICommand ApplySortCommand { get; }
        public ICommand LoadMoreItemsCommand { get; }

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
                IsBusy = true;
                IsRefreshing = true;
                IsLoadingMore = false;
                _currentPage = 1;

                // Reset collections
                Vehicles.Clear();
                FilteredVehicles.Clear();
                GroupedVehicles.Clear();

                // Generate mock data (would be replaced by API call)
                var mockVehicles = GenerateMockVehicles(100); // Generate larger dataset
                Vehicles.AddRange(mockVehicles);
                
                // Apply filtering, sorting and grouping
                await ApplyFiltersAndSortAsync();
                
                LastUpdated = DateTime.Now;
                OnPropertyChanged(nameof(TotalVehicleCount));
                OnPropertyChanged(nameof(FilteredVehicleCount));
            }
            catch (Exception ex)
            {
                // In a real app, we'd log this error
                Console.WriteLine($"Error refreshing vehicles: {ex}");
            }
            finally
            {
                IsRefreshing = false;
                IsBusy = false;
            }
        }

        private List<TransportVehicle> GenerateMockVehicles(int count = 50)
        {
            var random = new Random();
            var vehicles = new List<TransportVehicle>();
            
            var types = new[] { "Bus", "Train", "Tram", "Subway", "Ferry" };
            var statuses = new[] { "On Time", "Delayed", "Slight Delay", "Significant Delay", "Arriving", "Departing", "Out of Service" };
            var routes = new[] 
            { 
                "Downtown Express", "Airport Link", "Circular Line", "Cross City", 
                "Harbor Route", "University Line", "Stadium Express", "Beach Link", 
                "Hospital Route", "Shopping Center", "Industrial Zone" 
            };
            
            for (int i = 1; i <= count; i++)
            {
                var type = types[random.Next(types.Length)];
                var routeNumber = random.Next(1, 40);
                var routeName = routes[random.Next(routes.Length)];
                var status = statuses[random.Next(statuses.Length)];
                var capacity = random.Next(50, 250);
                
                // Generate more realistic next arrival info
                string nextArrivalInfo = "";
                if (random.Next(100) < 80) // 80% of vehicles have next stop info
                {
                    var minutesToArrival = random.Next(1, 20);
                    var nextStop = $"Stop {random.Next(101, 199)}";
                    nextArrivalInfo = $"Arriving at {nextStop} in {minutesToArrival} min";
                }
                
                vehicles.Add(new TransportVehicle
                {
                    Id = Guid.NewGuid().ToString(),
                    Number = $"{type[0]}-{routeNumber:D2}{random.Next(10, 99)}",
                    Type = type,
                    Route = $"{routeNumber} - {routeName}",
                    NextStop = $"Stop {random.Next(101, 199)}",
                    NextArrivalInfo = nextArrivalInfo,
                    Status = status,
                    Capacity = capacity,
                    Occupancy = random.Next((int)(capacity * 0.1), (int)(capacity * 0.9)),
                    LastUpdated = DateTime.Now.AddMinutes(-random.Next(1, 30)).AddSeconds(-random.Next(0, 59)),
                    Latitude = 51.5 + (random.NextDouble() - 0.5) * 0.1,
                    Longitude = -0.12 + (random.NextDouble() - 0.5) * 0.1,
                    Speed = random.Next(0, 80)
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
            await _navigationService.NavigateToAsync(nameof(VehicleDetailsPage), new Dictionary<string, object>
            {
                { "vehicleId", vehicle.Id }
            });
        }

        private async Task LoadMoreItemsAsync()
        {
            if (IsBusy || IsLoadingMore)
                return;

            try
            {
                IsLoadingMore = true;
                
                // Simulate network delay
                await Task.Delay(800);
                
                // In a real app, this would be an API call with pagination
                _currentPage++;
                var additionalVehicles = GenerateMockVehicles(20); // Load 20 more items
                Vehicles.AddRange(additionalVehicles);
                
                // Apply filtering and sorting to the new items
                await ApplyFiltersAndSortAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading more vehicles: {ex}");
            }
            finally
            {
                IsLoadingMore = false;
            }
        }

        private async Task ApplyFiltersAndSortAsync()
        {
            await Task.Run(() =>
            {
                // Apply filters
                var filtered = Vehicles.Where(v => 
                    // Apply transport type filters
                    (_transportFilters.TryGetValue(v.Type, out bool typeEnabled) && typeEnabled) &&
                    
                    // Apply status filters
                    (_statusFilters.TryGetValue(v.Status, out bool statusEnabled) && statusEnabled) &&
                    
                    // Apply search text if provided
                    (string.IsNullOrWhiteSpace(SearchText) || 
                     v.Number.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                     v.Route.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                     v.Type.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                     v.Status.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                ).ToList();

                // Apply sorting
                filtered = ApplySorting(filtered);
                
                // Update filtered vehicles collection
                FilteredVehicles.ReplaceRange(filtered);
                
                // Create groups based on current sort option
                var grouped = CreateGroups(filtered);
                GroupedVehicles.Clear();
                foreach (var group in grouped)
                {
                    GroupedVehicles.Add(group);
                }
            });
        }

        private List<TransportVehicle> ApplySorting(List<TransportVehicle> vehicles)
        {
            return SortOption switch
            {
                "Type" => vehicles.OrderBy(v => v.Type)
                                  .ThenBy(v => v.Route)
                                  .ThenBy(v => v.Number)
                                  .ToList(),
                                  
                "Route" => vehicles.OrderBy(v => v.Route)
                                  .ThenBy(v => v.Number)
                                  .ToList(),
                                  
                "Status" => vehicles.OrderBy(v => v.Status)
                                   .ThenBy(v => v.Type)
                                   .ThenBy(v => v.Number)
                                   .ToList(),
                                   
                "Last Updated" => vehicles.OrderByDescending(v => v.LastUpdated)
                                       .ToList(),
                                       
                _ => vehicles.OrderBy(v => v.Type)
                            .ThenBy(v => v.Route)
                            .ToList()
            };
        }

        private List<Grouping<string, TransportVehicle>> CreateGroups(List<TransportVehicle> vehicles)
        {
            return SortOption switch
            {
                "Type" => vehicles.GroupBy(v => v.Type)
                                 .Select(g => new Grouping<string, TransportVehicle>(g.Key, g))
                                 .ToList(),
                                 
                "Route" => vehicles.GroupBy(v => v.Route.Split('-')[0].Trim())
                                  .Select(g => new Grouping<string, TransportVehicle>($"Route {g.Key}", g))
                                  .ToList(),
                                  
                "Status" => vehicles.GroupBy(v => v.Status)
                                   .Select(g => new Grouping<string, TransportVehicle>(g.Key, g))
                                   .ToList(),
                                   
                "Last Updated" => vehicles.GroupBy(v => GetTimeGroup(v.LastUpdated))
                                      .Select(g => new Grouping<string, TransportVehicle>(g.Key, g))
                                      .ToList(),
                                      
                _ => vehicles.GroupBy(v => v.Type)
                           .Select(g => new Grouping<string, TransportVehicle>(g.Key, g))
                           .ToList()
            };
        }

        private string GetTimeGroup(DateTime time)
        {
            var timeDiff = DateTime.Now - time;
            
            if (timeDiff.TotalMinutes < 5)
                return "Just now (< 5 min)";
            if (timeDiff.TotalMinutes < 15)
                return "Recently (< 15 min)";
            if (timeDiff.TotalMinutes < 30)
                return "Last half hour";
            if (timeDiff.TotalMinutes < 60)
                return "Last hour";
            
            return "Over an hour ago";
        }
        
        private void ToggleFilters()
        {
            AreFiltersVisible = !AreFiltersVisible;
        }
        
        private void ToggleTransportFilter(string transportType)
        {
            if (_transportFilters.ContainsKey(transportType))
            {
                _transportFilters[transportType] = !_transportFilters[transportType];
                RefreshCommand.Execute(null);
            }
        }
        
        private void ToggleStatusFilter(string status)
        {
            if (_statusFilters.ContainsKey(status))
            {
                _statusFilters[status] = !_statusFilters[status];
                RefreshCommand.Execute(null);
            }
        }
        
        private void ApplySort()
        {
            if (FilteredVehicles?.Count > 0)
            {
                ApplyFiltersAndSortAsync().ConfigureAwait(false);
            }
        }

        private void ExecuteSearch(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                ClearSearch();
                return;
            }

            // Apply filtering based on query
            ApplyFiltersAndSortAsync().ConfigureAwait(false);
        }

        private void ClearSearch()
        {
            SearchText = string.Empty;
            // Reset search and apply filters
            ApplyFiltersAndSortAsync().ConfigureAwait(false);
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
