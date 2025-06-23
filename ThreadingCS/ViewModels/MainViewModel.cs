using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui;
using ThreadingCS.Models;
using ThreadingCS.Services;

namespace ThreadingCS.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly TransportApiService _apiService;
        private readonly DataProcessingService _processingService;
        private CancellationTokenSource _cancellationTokenSource;
        private List<TransportRoute> _largeDataset;
        private bool _isLoading;
        private string _searchTerm;
        private string _statusMessage;
        private int _processedRecords;
        private double _progressValue;
        private double _maxDuration = 60;
        private double _maxDistance = 10;

        public ObservableCollection<TransportRoute> Routes { get; } = new();
        public ObservableCollection<TransportRoute> FilteredRoutes { get; } = new();

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged();
            }
        }

        public string SearchTerm
        {
            get => _searchTerm;
            set
            {
                _searchTerm = value;
                OnPropertyChanged();
                _ = ApplySearchFilterAsync();
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        public int ProcessedRecords
        {
            get => _processedRecords;
            set
            {
                _processedRecords = value;
                OnPropertyChanged();
            }
        }

        public double ProgressValue
        {
            get => _progressValue;
            set
            {
                _progressValue = value;
                OnPropertyChanged();
            }
        }

        public double MaxDuration
        {
            get => _maxDuration;
            set
            {
                _maxDuration = value;
                OnPropertyChanged();
                _ = ApplyDurationFilterAsync();
            }
        }

        public double MaxDistance
        {
            get => _maxDistance;
            set
            {
                _maxDistance = value;
                OnPropertyChanged();
                _ = ApplyDistanceFilterAsync();
            }
        }

        public MainViewModel()
        {
            _apiService = new TransportApiService();
            _processingService = new DataProcessingService();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public async Task InitializeAsync()
        {
            IsLoading = true;
            StatusMessage = "Loading data...";

            try
            {
                // Fetch sample routes (API call)
                var response = await _apiService.GetRoutesAsync(51.507198, -0.136512, 51.505983, -0.017931);
                
                if (response.IsSuccess && response.Routes.Any())
                {
                    MainThread.BeginInvokeOnMainThread(() => {
                        Routes.Clear();
                        foreach (var route in response.Routes)
                        {
                            Routes.Add(route);
                        }
                    });
                    
                    StatusMessage = $"Loaded {Routes.Count} routes";
                }
                else
                {
                    StatusMessage = "Failed to load routes: " + response.ErrorMessage;
                }

                // Generate large dataset in background thread for PLINQ demonstrations
                await Task.Run(() =>
                {
                    StatusMessage = "Generating large dataset...";
                    _largeDataset = _apiService.GenerateLargeDataset(100000);
                    StatusMessage = $"Generated {_largeDataset.Count:N0} records";
                });
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task StartMonitoringAsync()
        {
            if (_cancellationTokenSource?.IsCancellationRequested ?? true)
            {
                _cancellationTokenSource = new CancellationTokenSource();
            }

            var token = _cancellationTokenSource.Token;
            
            await Task.Run(async () =>
            {
                try
                {
                    StatusMessage = "Started real-time monitoring";
                    
                    while (!token.IsCancellationRequested)
                    {
                        // Simulate API calls in parallel to fetch vehicle positions
                        var tasks = new List<Task>();
                        
                        foreach (var route in Routes)
                        {
                            tasks.Add(Task.Run(async () =>
                            {
                                // Simulate updating vehicle positions
                                await UpdateVehiclePositionsAsync(route);
                            }, token));
                        }
                        
                        await Task.WhenAll(tasks);
                        await Task.Delay(2000, token); // Update every 2 seconds
                    }
                }
                catch (OperationCanceledException)
                {
                    StatusMessage = "Monitoring canceled";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Monitoring error: {ex.Message}";
                }
            }, token);
        }

        public void StopMonitoring()
        {
            _cancellationTokenSource?.Cancel();
            StatusMessage = "Stopped monitoring";
        }

        public async Task ProcessLargeDatasetAsync()
        {
            if (_largeDataset == null || !_largeDataset.Any())
            {
                StatusMessage = "No dataset available";
                return;
            }

            IsLoading = true;
            StatusMessage = "Processing large dataset with PLINQ...";
            ProcessedRecords = 0;
            ProgressValue = 0;

            try
            {
                // Process the large dataset in batches using PLINQ
                await Task.Run(async () =>
                {
                    var batchSize = 10000;
                    var batchCount = (int)Math.Ceiling(_largeDataset.Count / (double)batchSize);
                    
                    for (int i = 0; i < batchCount; i++)
                    {
                        var startIndex = i * batchSize;
                        var count = Math.Min(batchSize, _largeDataset.Count - startIndex);
                        var batch = _largeDataset.GetRange(startIndex, count);
                        
                        // Use PLINQ to process the batch
                        var processed = batch.AsParallel()
                            .Where(r => r.Duration <= MaxDuration && r.Distance <= MaxDistance)
                            .ToList();
                        
                        ProcessedRecords += processed.Count;
                        ProgressValue = (double)(i + 1) / batchCount;
                        
                        // Update status message occasionally
                        if (i % 5 == 0 || i == batchCount - 1)
                        {
                            StatusMessage = $"Processing batch {i+1} of {batchCount}... Found {ProcessedRecords:N0} matching routes";
                        }
                        
                        // Small delay to demonstrate progress
                        await Task.Delay(10);
                    }
                    
                    StatusMessage = $"Processing complete. Found {ProcessedRecords:N0} routes matching criteria.";
                });

                // Update UI with a sample of matching routes
                var optimalRoutes = await Task.Run(() => 
                    _processingService.GetOptimalRoutes(_largeDataset, MaxDuration, MaxDistance)
                );

                MainThread.BeginInvokeOnMainThread(() => {
                    FilteredRoutes.Clear();
                    foreach (var route in optimalRoutes)
                    {
                        FilteredRoutes.Add(route);
                    }
                });
            }
            catch (Exception ex)
            {
                StatusMessage = $"Processing error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task UpdateVehiclePositionsAsync(TransportRoute route)
        {
            // Simulate vehicle movement
            var random = new Random();
            
            foreach (var vehicle in route.Vehicles)
            {
                // Small random changes to position to simulate movement
                vehicle.Latitude += (random.NextDouble() - 0.5) * 0.0005;
                vehicle.Longitude += (random.NextDouble() - 0.5) * 0.0005;
                vehicle.Bearing = (vehicle.Bearing + random.Next(-10, 10)) % 360;
                vehicle.LastUpdated = DateTime.Now;
            }
        }

        private async Task ApplySearchFilterAsync()
        {
            if (_largeDataset == null || !_largeDataset.Any())
                return;

            await Task.Run(() =>
            {
                var filteredRoutes = string.IsNullOrWhiteSpace(SearchTerm)
                    ? _largeDataset.Take(10).ToList()
                    : _processingService.SearchRoutesByName(_largeDataset, SearchTerm);
                
                MainThread.BeginInvokeOnMainThread(() => {
                    FilteredRoutes.Clear();
                    foreach (var route in filteredRoutes.Take(10))
                    {
                        FilteredRoutes.Add(route);
                    }
                    
                    StatusMessage = $"Found {filteredRoutes.Count:N0} routes matching '{SearchTerm}'";
                });
            });
        }

        private async Task ApplyDurationFilterAsync()
        {
            if (_largeDataset == null || !_largeDataset.Any())
                return;

            await Task.Run(() =>
            {
                var filteredRoutes = _processingService.FilterRoutesByDuration(_largeDataset, MaxDuration);
                
                MainThread.BeginInvokeOnMainThread(() => {
                    FilteredRoutes.Clear();
                    foreach (var route in filteredRoutes.Take(10))
                    {
                        FilteredRoutes.Add(route);
                    }
                    
                    StatusMessage = $"Found {filteredRoutes.Count:N0} routes under {MaxDuration} minutes";
                });
            });
        }

        private async Task ApplyDistanceFilterAsync()
        {
            if (_largeDataset == null || !_largeDataset.Any())
                return;

            await Task.Run(() =>
            {
                var filteredRoutes = _processingService.FilterRoutesByDistance(_largeDataset, MaxDistance);
                
                MainThread.BeginInvokeOnMainThread(() => {
                    FilteredRoutes.Clear();
                    foreach (var route in filteredRoutes.Take(10))
                    {
                        FilteredRoutes.Add(route);
                    }
                    
                    StatusMessage = $"Found {filteredRoutes.Count:N0} routes under {MaxDistance} km";
                });
            });
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
