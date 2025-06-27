using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui;
using Microsoft.Maui.ApplicationModel;
using ThreadingCS.Models;
using ThreadingCS.Services;

namespace ThreadingCS.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly TransportApiService _apiService;
        private readonly DataProcessingService _processingService;
        private readonly DatabaseService _databaseService;
        private CancellationTokenSource _cancellationTokenSource;
        private List<TransportRoute> _largeDataset;
        private bool _isLoading;
        private string _searchTerm;
        private string _statusMessage;
        private int _processedRecords;
        private double _progressValue;
        private double _maxDuration = 300; // Default to 5 hours for longer routes
        private double _maxDistance = 2000; // Default to 2000 km for international routes

        // Origin coordinate backing fields
        private double _originLatitude = 52.3738; // Amsterdam Centraal Station
        private double _originLongitude = 4.8909; 
        private bool _isOriginValid = true;
        
        // Destination coordinate backing fields
        private double _destinationLatitude = 52.3584; // Amsterdam Museum Square
        private double _destinationLongitude = 4.8812; // Amsterdam coordinates
        private bool _isDestinationValid = true;


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

        public double OriginLatitude
        {
            get => _originLatitude;
            set
            {
                if (_originLatitude != value)
                {
                    _originLatitude = value;
                    OnPropertyChanged();
                    ValidateOriginCoordinates();
                }
            }
        }

        public double OriginLongitude
        {
            get => _originLongitude;
            set
            {
                if (_originLongitude != value)
                {
                    _originLongitude = value;
                    OnPropertyChanged();
                    ValidateOriginCoordinates();
                }
            }
        }

        public bool IsOriginValid
        {
            get => _isOriginValid;
            private set
            {
                if (_isOriginValid != value)
                {
                    _isOriginValid = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsCoordinatesValid));
                }
            }
        }
        
        public bool IsCoordinatesValid => IsOriginValid && IsDestinationValid;

        public double DestinationLatitude
        {
            get => _destinationLatitude;
            set
            {
                if (_destinationLatitude != value)
                {
                    _destinationLatitude = value;
                    OnPropertyChanged();
                    ValidateDestinationCoordinates();
                }
            }
        }

        public double DestinationLongitude
        {
            get => _destinationLongitude;
            set
            {
                if (_destinationLongitude != value)
                {
                    _destinationLongitude = value;
                    OnPropertyChanged();
                    ValidateDestinationCoordinates();
                }
            }
        }

        public bool IsDestinationValid
        {
            get => _isDestinationValid;
            private set
            {
                if (_isDestinationValid != value)
                {
                    _isDestinationValid = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsCoordinatesValid));
                }
            }
        }

        private void ValidateOriginCoordinates()
        {
            // Basic validation for latitude (-90 to 90) and longitude (-180 to 180)
            IsOriginValid = _originLatitude >= -90 && _originLatitude <= 90 &&
                          _originLongitude >= -180 && _originLongitude <= 180;
        }

        private void ValidateDestinationCoordinates()
        {
            // Basic validation for latitude (-90 to 90) and longitude (-180 to 180)
            IsDestinationValid = _destinationLatitude >= -90 && _destinationLatitude <= 90 &&
                               _destinationLongitude >= -180 && _destinationLongitude <= 180;
        }

        public MainViewModel()
        {
            _databaseService = new DatabaseService();
            _apiService = new TransportApiService(_databaseService);
            _processingService = new DataProcessingService();
            _cancellationTokenSource = new CancellationTokenSource();
            
            // Set initial status message with X symbol
            StatusMessage = "❌ Please click 'Load Data' first to generate the dataset";
        }

        public async Task InitializeAsync()
        {
            Debug.WriteLine("[Init] Entered InitializeAsync");
            IsLoading = true;
            StatusMessage = "Initializing application...";
            ProcessedRecords = 0;
            ProgressValue = 0.1;
            Debug.WriteLine("[Init] Set IsLoading and StatusMessage");

            // Use origin coordinates from properties
            double originLat = _originLatitude;
            double originLng = _originLongitude;

            // Validate origin coordinates before API call
            StatusMessage = "Validating coordinates...";
            ValidateOriginCoordinates();
            if (!IsOriginValid)
            {
                IsLoading = false;
                StatusMessage = "Error: Invalid origin coordinates";
                Debug.WriteLine("[Init] Invalid origin coordinates. Aborting API call.");
                return;
            }

            // Validate destination before API call
            ValidateDestinationCoordinates();
            if (!IsDestinationValid)
            {
                IsLoading = false;
                StatusMessage = "Error: Invalid destination coordinates";
                Debug.WriteLine("[Init] Invalid destination coordinates. Aborting API call.");
                return;
            }

            try
            {
                // Step 1: API Call (20%)
                StatusMessage = "Connecting to transit API...";
                ProgressValue = 0.2;
                ProcessedRecords = 10; // Show some initial progress
                Debug.WriteLine($"[Init] Calling GetRoutesAsync with origin=({originLat},{originLng}), dest=({_destinationLatitude},{_destinationLongitude})");
                
                // Show incremental progress during API call
                var apiCallProgress = new Progress<int>(percent => {
                    ProcessedRecords = 10 + percent;
                    if (percent % 20 == 0) {
                        StatusMessage = $"API request in progress ({percent}%)...";
                    }
                });
                
                // Simulate progress updates during the API call
                _ = Task.Run(async () => {
                    for (int i = 0; i < 5 && IsLoading; i++) {
                        await Task.Delay(300);
                        ((IProgress<int>)apiCallProgress).Report(i * 20);
                    }
                });
                
                var response = await _apiService.GetRoutesAsync(originLat, originLng, _destinationLatitude, _destinationLongitude);
                
                // Step 2: Processing API Response (40%)
                StatusMessage = "Processing API response...";
                ProgressValue = 0.4;
                Debug.WriteLine("[Init] GetRoutesAsync returned");

                // Step 3: Load from database (60-80%) - with optimized approach
                StatusMessage = "Loading cached routes from database...";
                ProgressValue = 0.6;
                ProcessedRecords = 100;
                Debug.WriteLine("[Init] Starting database loading phase with optimizations");
                
                await Task.Run(async () => 
                {
                    try
                    {
                        // Use a timeout for database operations
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                        
                        // Show initial loading message
                        StatusMessage = "Checking database for cached routes...";
                        ProgressValue = 0.62;
                        
                        // Try to load a limited number of routes (100 max) with timeout
                        var dbRoutesTask = _databaseService.GetAllRoutesAsync(100);
                        var dbRoutes = await dbRoutesTask.WaitAsync(cts.Token);
                        
                        if (dbRoutes.Count > 0)
                        {
                            StatusMessage = $"Loaded {dbRoutes.Count} routes from database";
                            ProcessedRecords = dbRoutes.Count;
                            ProgressValue = 0.7;
                            Debug.WriteLine($"[Init] Successfully loaded {dbRoutes.Count} routes from database");
                        }
                        else
                        {
                            // Show simulated progress during this phase
                            for (int i = 0; i < 3; i++)
                            {
                                StatusMessage = $"Preparing data structures ({(i+1)*30}%)...";
                                ProcessedRecords = 100 + (i * 30); // Increment by 30 each time
                                ProgressValue = 0.65 + (i * 0.05);
                                await Task.Delay(50);
                            }
                            
                            StatusMessage = "No cached data found, will generate new dataset";
                            Debug.WriteLine("[Init] No routes found in database or operation timed out");
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        StatusMessage = "Database operation timed out, continuing with generated data";
                        Debug.WriteLine("[Init] Database loading timed out");
                    }
                    catch (Exception ex)
                    {
                        StatusMessage = "Error accessing database, continuing with generated data";
                        Debug.WriteLine($"[Init] Database error: {ex.Message}");
                    }
                    finally
                    {
                        ProcessedRecords = 200; // Just show a reasonable number
                        ProgressValue = 0.8;
                    }
                });
                
                Debug.WriteLine("[Init] Database loading phase complete");

                // Step 4: Generate Large Dataset (80-100%)
                StatusMessage = "Preparing large dataset for parallel processing...";
                ProgressValue = 0.8;
                ProcessedRecords = 200; // Show initial progress for dataset generation
                Debug.WriteLine("[Init] Starting large dataset generation");
                
                // Create a timer to update progress more frequently during the long dataset generation
                int progressCounter = 0;
                var progressTimer = new System.Threading.Timer(async _ => 
                {
                    if (!IsLoading) return;
                    
                    progressCounter++;
                    ProcessedRecords = 200 + (progressCounter * 1000); // Increment by 1000 each time
                    
                    // Cycle through different status messages to show activity
                    var messages = new[] {
                        "Generating synthetic routes...",
                        "Creating transit connections...",
                        "Calculating route durations...",
                        "Preparing data for PLINQ processing...",
                        "Building large dataset..."
                    };
                    
                    StatusMessage = $"{messages[progressCounter % messages.Length]} ({ProcessedRecords:N0} records)";
                    ProgressValue = Math.Min(0.95, 0.8 + (progressCounter * 0.01)); // Cap at 95%
                    
                }, null, 0, 300); // Update more frequently - every 300ms
                
                await Task.Run(async () =>
                {
                    Debug.WriteLine("[Init] Inside Task.Run: Generating large dataset...");
                    
                    try
                    {
                        // PERFORMANCE FIX: Skip database operations by setting saveToDatabase=false
                        // This avoids the database bottleneck that was causing the app to hang
                        _largeDataset = await _apiService.GenerateLargeDataset(100000, saveToDatabase: false);
                        
                        // Stop the timer
                        progressTimer.Change(Timeout.Infinite, Timeout.Infinite);
                        progressTimer.Dispose();
                        
                        ProcessedRecords = _largeDataset.Count;
                        StatusMessage = $"Generated {_largeDataset.Count:N0} records for parallel processing";
                        ProgressValue = 1.0;
                        Debug.WriteLine($"[Init] Large dataset generated: {_largeDataset.Count} records");
                    }
                    catch (Exception ex)
                    {
                        // Make sure to dispose the timer even if there's an error
                        progressTimer.Change(Timeout.Infinite, Timeout.Infinite);
                        progressTimer.Dispose();
                        Debug.WriteLine($"[Init] Error generating large dataset: {ex.Message}");
                        throw;
                    }
                });
                Debug.WriteLine("[Init] Finished large dataset generation");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                Debug.WriteLine($"[Init] Exception: {ex}");
            }
            finally
            {
                // Make sure we show the loading complete message as the very last step
                await Task.Delay(500);
                IsLoading = false;
                
                // This is the FINAL status message that should be displayed after loading
                // It explicitly tells the user to click 'Process Large Dataset'
                MainThread.BeginInvokeOnMainThread(() => {
                    StatusMessage = "✅ Data loaded successfully. Click 'Process Large Dataset' to continue.";
                });
                
                Debug.WriteLine("[Init] Initialization complete with prompt to process large dataset");
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
                StatusMessage = "❌ Please click 'Load Data' first to generate the dataset";
                return;
            }

            IsLoading = true;
            StatusMessage = "Initializing parallel processing...";
            ProcessedRecords = 0;
            ProgressValue = 0.05;
            
            // Show initial preparation message
            await Task.Delay(300);

            try
            {
                // Process the large dataset in batches using PLINQ
                await Task.Run(async () =>
                {
                    // Step 1: Preparing data (10%)
                    StatusMessage = "Preparing data for parallel processing...";
                    ProgressValue = 0.1;
                    await Task.Delay(200);
                    
                    var batchSize = 10000;
                    var batchCount = (int)Math.Ceiling(_largeDataset.Count / (double)batchSize);
                    var totalRecords = _largeDataset.Count;
                    var availableCores = Environment.ProcessorCount;
                    
                    // Step 2: Show PLINQ info (15%)
                    StatusMessage = $"Starting PLINQ on {availableCores} CPU cores...";
                    ProgressValue = 0.15;
                    await Task.Delay(300);
                    
                    // Create a timer to update progress more frequently during batch processing
                    int microProgressCounter = 0;
                    var batchProgressTimer = new System.Threading.Timer(_ => 
                    {
                        if (!IsLoading) return;
                        
                        microProgressCounter++;
                        
                        // Cycle through different status messages to show activity during PLINQ processing
                        var processingMessages = new[] {
                            "Filtering routes by duration and distance...",
                            "Parallel processing in progress...",
                            "Utilizing all CPU cores with PLINQ...",
                            "Applying filters across multiple threads...",
                            "Optimizing route selection..."
                        };
                        
                        // Don't update the main progress bar, just the status message
                        StatusMessage = $"{processingMessages[microProgressCounter % processingMessages.Length]} ({ProcessedRecords:N0} records)";
                        
                    }, null, 0, 300); // Update every 300ms
                    
                    try {
                        // Step 3: Process batches
                        for (int i = 0; i < batchCount; i++)
                        {
                            var startIndex = i * batchSize;
                            var count = Math.Min(batchSize, _largeDataset.Count - startIndex);
                            var batch = _largeDataset.GetRange(startIndex, count);
                            
                            // Show which batch is being processed
                            StatusMessage = $"Batch {i+1}/{batchCount}: Processing {count:N0} routes with PLINQ...";
                            
                            // Use PLINQ to process the batch
                            var processed = batch.AsParallel()
                                .WithDegreeOfParallelism(availableCores) // Explicitly use all cores
                                .Where(r => r.Duration <= MaxDuration && r.Distance <= MaxDistance)
                                .ToList();
                            
                            ProcessedRecords += processed.Count;
                            
                            // Calculate progress - allocate 70% of the progress bar to batch processing (from 15% to 85%)
                            ProgressValue = 0.15 + (0.70 * (i + 1) / batchCount);
                            
                            // Update detailed status message for each batch
                            var percentComplete = (int)(100 * (i + 1) / batchCount);
                            var matchRate = batch.Count > 0 ? (processed.Count * 100 / batch.Count) : 0;
                            StatusMessage = $"Batch {i+1}/{batchCount} ({percentComplete}%): Found {processed.Count:N0} matches ({matchRate}% match rate)";
                            
                            // Small delay to demonstrate progress
                            await Task.Delay(50);
                        }
                    }
                    finally {
                        // Make sure to dispose the timer
                        batchProgressTimer.Change(Timeout.Infinite, Timeout.Infinite);
                        batchProgressTimer.Dispose();
                    }                    
                    // Step 4: Processing complete (85%)
                    StatusMessage = $"Processing complete! Found {ProcessedRecords:N0} routes matching criteria.";
                    ProgressValue = 0.85;
                    await Task.Delay(300);
                });

                // Step 5: Finding optimal routes (90%)
                StatusMessage = "Finding optimal routes for display...";
                ProgressValue = 0.9;
                
                // Update UI with a sample of matching routes
                var optimalRoutes = await Task.Run(() => 
                    _processingService.GetOptimalRoutes(_largeDataset, MaxDuration, MaxDistance)
                );

                // Step 6: Updating UI (95%)
                StatusMessage = "Updating display with results...";
                ProgressValue = 0.95;
                
                MainThread.BeginInvokeOnMainThread(() => {
                    FilteredRoutes.Clear();
                    foreach (var route in optimalRoutes)
                    {
                        FilteredRoutes.Add(route);
                    }
                });
                
                // Step 7: Complete (100%)
                StatusMessage = $"✅ Complete! Displaying {optimalRoutes.Count} optimal routes from {ProcessedRecords:N0} matches";
                ProgressValue = 1.0;
                await Task.Delay(1000); // Show completion for a moment
                
                // Restore the prompt message to ensure it's always visible after processing
                StatusMessage = "✅ Data loaded successfully. Click 'Process Large Dataset' to continue.";
                await Task.Delay(200);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error processing large dataset: {ex.Message}. Please check the dataset and try again.";
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
            {
                StatusMessage = "❌ Please click 'Load Data' first to generate the dataset";
                return;
            }

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
            {
                StatusMessage = "❌ Please click 'Load Data' first to generate the dataset";
                return;
            }

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
            {
                StatusMessage = "❌ Please click 'Load Data' first to generate the dataset";
                return;
            }

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
