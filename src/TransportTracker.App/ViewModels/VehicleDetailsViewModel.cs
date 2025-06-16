using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using TransportTracker.App.Core.MVVM;
using TransportTracker.App.Services;
using TransportTracker.App.Views.Maps;

namespace TransportTracker.App.ViewModels
{
    /// <summary>
    /// View model for the vehicle details page with live updates
    /// </summary>
    public class VehicleDetailsViewModel : BaseViewModel, IQueryAttributable
    {
        private readonly INavigationService _navigationService;
        private readonly IVehiclesService _vehiclesService;
        
        private string _vehicleId;
        private TransportVehicle _vehicle;
        private bool _isLoadingMap;
        private string _nextStopEta;
        private string _delayText;
        private bool _isDelayed;
        private ObservableCollection<StopInfo> _nextStops;
        private string _vehicleIcon;
        private CancellationTokenSource _liveUpdatesCts;
        
        /// <summary>
        /// Gets or sets the vehicle being displayed
        /// </summary>
        public TransportVehicle Vehicle
        {
            get => _vehicle;
            set => SetProperty(ref _vehicle, value);
        }
        
        /// <summary>
        /// Gets or sets a value indicating whether the map is loading
        /// </summary>
        public bool IsLoadingMap
        {
            get => _isLoadingMap;
            set => SetProperty(ref _isLoadingMap, value);
        }
        
        /// <summary>
        /// Gets or sets the ETA to the next stop
        /// </summary>
        public string NextStopEta
        {
            get => _nextStopEta;
            set => SetProperty(ref _nextStopEta, value);
        }
        
        /// <summary>
        /// Gets or sets the delay text
        /// </summary>
        public string DelayText
        {
            get => _delayText;
            set => SetProperty(ref _delayText, value);
        }
        
        /// <summary>
        /// Gets or sets a value indicating whether the vehicle is delayed
        /// </summary>
        public bool IsDelayed
        {
            get => _isDelayed;
            set => SetProperty(ref _isDelayed, value);
        }
        
        /// <summary>
        /// Gets or sets the collection of upcoming stops
        /// </summary>
        public ObservableCollection<StopInfo> NextStops
        {
            get => _nextStops;
            set => SetProperty(ref _nextStops, value);
        }
        
        /// <summary>
        /// Gets or sets the vehicle icon path
        /// </summary>
        public string VehicleIcon
        {
            get => _vehicleIcon;
            set => SetProperty(ref _vehicleIcon, value);
        }
        
        /// <summary>
        /// Gets the command to initialize the view model
        /// </summary>
        public ICommand InitializeCommand { get; }
        
        /// <summary>
        /// Gets the command to clean up resources
        /// </summary>
        public ICommand CleanupCommand { get; }
        
        /// <summary>
        /// Gets the command to show the vehicle on the map
        /// </summary>
        public ICommand ShowOnMapCommand { get; }
        
        /// <summary>
        /// Gets the command to refresh the vehicle data
        /// </summary>
        public ICommand RefreshCommand { get; }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="VehicleDetailsViewModel"/> class
        /// </summary>
        /// <param name="navigationService">The navigation service</param>
        /// <param name="vehiclesService">The vehicles service</param>
        public VehicleDetailsViewModel(INavigationService navigationService, IVehiclesService vehiclesService)
        {
            _navigationService = navigationService;
            _vehiclesService = vehiclesService;
            
            Title = "Vehicle Details";
            NextStops = new ObservableCollection<StopInfo>();
            
            InitializeCommand = CreateAsyncCommand(InitializeAsync);
            CleanupCommand = CreateCommand(Cleanup);
            ShowOnMapCommand = CreateAsyncCommand(ShowOnMapAsync);
            RefreshCommand = CreateAsyncCommand(RefreshAsync);
        }
        
        /// <summary>
        /// Applies the query parameters from navigation
        /// </summary>
        /// <param name="query">The query parameters</param>
        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("id", out var id) && id is string vehicleId)
            {
                _vehicleId = vehicleId;
                
                if (!IsLoading)
                {
                    // Load the vehicle data when ID is received
                    InitializeCommand.Execute(null);
                }
            }
        }
        
        private async Task InitializeAsync()
        {
            if (string.IsNullOrEmpty(_vehicleId))
                return;
                
            try
            {
                IsLoading = true;
                IsLoadingMap = true;
                
                // Load vehicle details
                var vehicle = await _vehiclesService.GetVehicleByIdAsync(_vehicleId);
                if (vehicle == null)
                {
                    await _navigationService.DisplayAlert("Error", "Vehicle not found", "OK");
                    await _navigationService.GoBackAsync();
                    return;
                }
                
                Vehicle = vehicle;
                Title = $"{vehicle.Type} {vehicle.Number}";
                
                // Set vehicle icon based on type
                VehicleIcon = vehicle.Type.ToLowerInvariant() switch
                {
                    "bus" => "bus_icon.png",
                    "train" => "train_icon.png",
                    "tram" => "tram_icon.png",
                    "subway" => "subway_icon.png",
                    "ferry" => "ferry_icon.png",
                    _ => "vehicle_icon.png"
                };
                
                UpdateVehicleInfo(vehicle);
                
                // Start live updates
                StartLiveUpdates();
                
                IsLoadingMap = false;
            }
            catch (Exception ex)
            {
                await _navigationService.DisplayAlert("Error", $"Failed to load vehicle details: {ex.Message}", "OK");
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        private void Cleanup()
        {
            // Cancel live updates
            _liveUpdatesCts?.Cancel();
            _liveUpdatesCts?.Dispose();
            _liveUpdatesCts = null;
        }
        
        private async Task ShowOnMapAsync()
        {
            if (Vehicle == null)
                return;
                
            // Navigate to map view and center on this vehicle
            await _navigationService.NavigateToAsync("///MapsTab", new Dictionary<string, object>
            {
                { "vehicleId", Vehicle.Id },
                { "zoomLevel", 15 }
            });
        }
        
        private async Task RefreshAsync()
        {
            if (string.IsNullOrEmpty(_vehicleId))
            {
                IsRefreshing = false;
                return;
            }
            
            try
            {
                // Reload vehicle data
                var vehicle = await _vehiclesService.GetVehicleByIdAsync(_vehicleId);
                if (vehicle != null)
                {
                    Vehicle = vehicle;
                    UpdateVehicleInfo(vehicle);
                }
            }
            catch (Exception ex)
            {
                // Log error but don't show alert during refresh
                System.Diagnostics.Debug.WriteLine($"Error refreshing vehicle: {ex.Message}");
            }
            finally
            {
                IsRefreshing = false;
            }
        }
        
        private void StartLiveUpdates()
        {
            // Cancel any existing updates
            Cleanup();
            
            // Create new cancellation token source
            _liveUpdatesCts = new CancellationTokenSource();
            
            // Start task to update vehicle data periodically
            Task.Run(async () =>
            {
                while (!_liveUpdatesCts.Token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(5000, _liveUpdatesCts.Token); // Update every 5 seconds
                        
                        // Skip if we're already refreshing
                        if (IsRefreshing)
                            continue;
                            
                        // Get updated vehicle data
                        var vehicle = await _vehiclesService.GetVehicleByIdAsync(_vehicleId);
                        if (vehicle != null)
                        {
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                // Update vehicle with fresh data
                                Vehicle = vehicle;
                                UpdateVehicleInfo(vehicle);
                            });
                        }
                    }
                    catch (TaskCanceledException)
                    {
                        // Normal cancellation, ignore
                        break;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error in live updates: {ex.Message}");
                        // Brief pause before retrying on error
                        await Task.Delay(1000, _liveUpdatesCts.Token);
                    }
                }
            }, _liveUpdatesCts.Token);
        }
        
        private void UpdateVehicleInfo(TransportVehicle vehicle)
        {
            if (vehicle == null)
                return;
                
            // Update ETA
            if (!string.IsNullOrEmpty(vehicle.NextArrivalInfo))
            {
                NextStopEta = vehicle.NextArrivalInfo;
            }
            else
            {
                // Generate mock ETA if not available
                var minutesToArrival = new Random().Next(1, 15);
                NextStopEta = $"{minutesToArrival} min";
                vehicle.NextArrivalInfo = NextStopEta;
            }
            
            // Update delay info
            var delayMinutes = 0;
            if (vehicle.IsDelayed)
            {
                delayMinutes = new Random().Next(2, 20);
                DelayText = $"{delayMinutes} minutes late";
                IsDelayed = true;
            }
            else
            {
                DelayText = "On time";
                IsDelayed = false;
            }
            
            // Update next stops
            UpdateNextStops(vehicle);
        }
        
        private void UpdateNextStops(TransportVehicle vehicle)
        {
            // Clear existing stops
            NextStops.Clear();
            
            // For now, generate mock stops
            var random = new Random();
            var stopCount = random.Next(3, 6);
            var currentTime = DateTime.Now;
            
            for (int i = 0; i < stopCount; i++)
            {
                var minutesAhead = i == 0 ? random.Next(1, 10) : (i * 5) + random.Next(1, 5);
                var arrivalTime = currentTime.AddMinutes(minutesAhead);
                
                NextStops.Add(new StopInfo
                {
                    Name = $"Stop {i + 1}",
                    Location = $"Street {(char)('A' + i)}",
                    EstimatedTime = arrivalTime.ToString("HH:mm"),
                    IsNext = i == 0
                });
            }
        }
    }
    
    /// <summary>
    /// Represents information about a transit stop
    /// </summary>
    public class StopInfo : ObservableObject
    {
        private string _name;
        private string _location;
        private string _estimatedTime;
        private bool _isNext;
        
        /// <summary>
        /// Gets or sets the name of the stop
        /// </summary>
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }
        
        /// <summary>
        /// Gets or sets the location of the stop
        /// </summary>
        public string Location
        {
            get => _location;
            set => SetProperty(ref _location, value);
        }
        
        /// <summary>
        /// Gets or sets the estimated arrival time
        /// </summary>
        public string EstimatedTime
        {
            get => _estimatedTime;
            set => SetProperty(ref _estimatedTime, value);
        }
        
        /// <summary>
        /// Gets or sets a value indicating whether this is the next stop
        /// </summary>
        public bool IsNext
        {
            get => _isNext;
            set => SetProperty(ref _isNext, value);
        }
    }
}
