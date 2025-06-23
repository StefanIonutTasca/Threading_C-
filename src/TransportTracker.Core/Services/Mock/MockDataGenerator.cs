using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TransportTracker.Core.Models;
using TransportTracker.Core.Threading;
using TransportTracker.Core.Threading.Coordination;
using TransportTracker.Core.Threading.Synchronization;

namespace TransportTracker.Core.Services.Mock
{
    /// <summary>
    /// Implementation of the mock data generator that creates realistic transport data
    /// for development and testing, capable of generating 100,000+ records.
    /// Uses multithreading to efficiently generate and update data.
    /// </summary>
    public class MockDataGenerator : IMockDataGenerator, IDisposable
    {
        private readonly ILogger<MockDataGenerator> _logger;
        private readonly ILogger<ThreadCoordinator> _threadCoordinatorLogger;
        private readonly IThreadFactory _threadFactory;
        private readonly Random _random;
        private readonly AsyncLock _dataLock = new AsyncLock();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly Stopwatch _simulationStopwatch = new Stopwatch();
        private readonly List<Thread> _workThreads = new List<Thread>();

        private Thread _mainSimulationThread;
        private bool _isRunning;
        private bool _disposed;
        private DateTime _simulationStartTime;
        private DateTime _simulationCurrentTime;

        // Route colors for realistic route generation
        private readonly string[] _routeColors = new[] {
            "#FF0000", "#00FF00", "#0000FF", "#FFFF00", "#FF00FF",
            "#00FFFF", "#800000", "#008000", "#000080", "#808000",
            "#800080", "#008080", "#808080", "#C00000", "#00C000",
            "#0000C0", "#C0C000", "#C000C0", "#00C0C0", "#C0C0C0"
        };

        // Common route names for realistic generation
        private readonly string[] _routeNames = new[] {
            "Route", "Line", "Express", "Metro", "Shuttle",
            "Tram", "Bus", "Ferry", "Train", "Local"
        };

        // Common stop name elements for realistic generation
        private readonly string[] _stopNamePrefixes = new[] {
            "Central", "North", "South", "East", "West",
            "Main", "Downtown", "Uptown", "City", "Market",
            "Park", "River", "Lake", "Hill", "Valley",
            "Old", "New", "Upper", "Lower", "Mid"
        };

        private readonly string[] _stopNameSuffixes = new[] {
            "Station", "Terminal", "Stop", "Square", "Plaza",
            "Center", "Junction", "Crossing", "Avenue", "Street",
            "Road", "Boulevard", "Lane", "Drive", "Way",
            "Place", "Court", "Circle", "Mall", "Campus"
        };

        // Concurrent collections for thread-safe access to generated data
        private ConcurrentBag<Route> _routes = new ConcurrentBag<Route>();
        private ConcurrentBag<Stop> _stops = new ConcurrentBag<Stop>();
        private ConcurrentBag<Vehicle> _vehicles = new ConcurrentBag<Vehicle>();
        private ConcurrentBag<Schedule> _schedules = new ConcurrentBag<Schedule>();

        // Thread synchronization and signaling
        private AsyncCountdownEvent _initializationComplete;
        private ThreadCoordinator _coordinator;


        /// <summary>
        /// Gets or sets the configuration for the mock data generator
        /// </summary>
        public MockDataConfiguration Configuration { get; set; } = new MockDataConfiguration();

        /// <summary>
        /// Gets a value indicating whether the generator is currently running
        /// </summary>
        public bool IsRunning => _isRunning;

        /// <summary>
        /// Event raised when mock data has been updated
        /// </summary>
        public event EventHandler<MockDataUpdatedEventArgs> DataUpdated;

        /// <summary>
        /// Creates a new instance of the MockDataGenerator
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="threadCoordinatorLogger">Logger instance for thread coordinator</param>
        /// <param name="threadFactory">Thread factory for creating worker threads</param>
        public MockDataGenerator(
            ILogger<MockDataGenerator> logger,
            ILogger<ThreadCoordinator> threadCoordinatorLogger,
            IThreadFactory threadFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _threadCoordinatorLogger = threadCoordinatorLogger ?? throw new ArgumentNullException(nameof(threadCoordinatorLogger));
            _threadFactory = threadFactory ?? throw new ArgumentNullException(nameof(threadFactory));

            // Initialize random number generator with seed if provided
            _random = Configuration.RandomSeed.HasValue
                ? new Random(Configuration.RandomSeed.Value)
                : new Random();

            _coordinator = new ThreadCoordinator(_threadCoordinatorLogger);
        }

        /// <summary>
        /// Starts the mock data generation process
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            using (await _dataLock.LockAsync(cancellationToken))
            {
                if (_isRunning)
                {
                    _logger.LogWarning("Mock data generator is already running");
                    return;
                }

                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(MockDataGenerator));
                }

                _logger.LogInformation("Starting mock data generator");

                // Link external cancellation to our token source
                if (cancellationToken != default)
                {
                    cancellationToken.Register(() => _cts.Cancel());
                }

                try
                {
                    // Initialize simulation time tracking
                    _simulationStartTime = DateTime.Now;
                    _simulationCurrentTime = _simulationStartTime;
                    _simulationStopwatch.Restart();

                    // Reset any existing data
                    _routes = new ConcurrentBag<Route>();
                    _stops = new ConcurrentBag<Stop>();
                    _vehicles = new ConcurrentBag<Vehicle>();
                    _schedules = new ConcurrentBag<Schedule>();

                    // Create synchronization primitives
                    _initializationComplete = new AsyncCountdownEvent(3); // Routes, stops, vehicles initial generation

                    // Create and start worker threads for initial data generation
                    _workThreads.Add(_threadFactory.CreateThread(GenerateRoutesThreadProc, "MockDataRoutes", true));
                    _workThreads.Add(_threadFactory.CreateThread(GenerateStopsThreadProc, "MockDataStops", true));
                    _workThreads.Add(_threadFactory.CreateThread(GenerateVehiclesThreadProc, "MockDataVehicles", true));

                    foreach (var thread in _workThreads)
                    {
                        thread.Start();
                    }

                    // Wait for initial data generation to complete
                    await _initializationComplete.WaitAsync(_cts.Token);

                    // Generate schedules on the current thread
                    var schedules = GenerateSchedules(_routes, _stops);
                    foreach (var schedule in schedules)
                    {
                        _schedules.Add(schedule);
                    }

                    _logger.LogInformation("Initial mock data generation complete: " +
                        $"{_routes.Count} routes, {_stops.Count} stops, " +
                        $"{_vehicles.Count} vehicles, {_schedules.Count} schedules");

                    // Start the main simulation thread for ongoing updates
                    _mainSimulationThread = _threadFactory.CreateThread(
                        SimulationThreadProc,
                        "MockDataSimulation",
                        true,
                        ThreadPriority.BelowNormal);

                    _mainSimulationThread.Start();

                    _isRunning = true;
                    _logger.LogInformation("Mock data generator started successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to start mock data generator");
                    await StopAsync();
                    throw;
                }
            }
        }

        /// <summary>
        /// Stops the mock data generation process
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task StopAsync()
        {
            using (await _dataLock.LockAsync())
            {
                if (!_isRunning)
                {
                    _logger.LogWarning("Mock data generator is not running");
                    return;
                }

                _logger.LogInformation("Stopping mock data generator");

                try
                {
                    // Signal cancellation
                    if (!_cts.IsCancellationRequested)
                    {
                        _cts.Cancel();
                    }

                    // Signal coordinator for any waiting operations
                    _coordinator.SignalAll();

                    // Wait for threads to complete gracefully (with timeout)
                    TimeSpan joinTimeout = TimeSpan.FromSeconds(5);
                    bool mainThreadJoined = _mainSimulationThread?.Join(joinTimeout) ?? true;

                    if (!mainThreadJoined)
                    {
                        _logger.LogWarning("Main simulation thread did not exit gracefully within timeout");
                    }

                    foreach (var thread in _workThreads)
                    {
                        if (!thread.Join(joinTimeout))
                        {
                            _logger.LogWarning($"Worker thread {thread.Name} did not exit gracefully within timeout");
                        }
                    }

                    _workThreads.Clear();
                    _simulationStopwatch.Stop();

                    _isRunning = false;
                    _logger.LogInformation("Mock data generator stopped");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while stopping mock data generator");
                    throw;
                }
            }
        }
    
        #region Data Generation Thread Procedures
    
        /// <summary>
        /// Thread procedure for generating routes
        /// </summary>
        private void GenerateRoutesThreadProc()
        {
            try
            {
                _logger.LogInformation("Starting route generation thread");
                var routes = GenerateRoutes(Configuration.RouteCount);
                foreach (var route in routes)
                {
                    _routes.Add(route);
                }
                _logger.LogInformation($"Generated {_routes.Count} routes");

                // Signal that route generation is complete
                _initializationComplete.Signal();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in route generation thread");
                _cts.Cancel(); // Cancel overall operation on error
            }
        }

        /// <summary>
        /// Thread procedure for generating stops
        /// </summary>
        private void GenerateStopsThreadProc()
        {
            try
            {
                // Wait for routes to be available before generating stops
                while (_routes.IsEmpty && !_cts.IsCancellationRequested)
                {
                    Thread.Sleep(100);
                }

                if (_cts.IsCancellationRequested)
                {
                    return;
                }

                _logger.LogInformation("Starting stop generation thread");
                var stops = GenerateStops(_routes, Configuration.AverageStopsPerRoute);
                foreach (var stop in stops)
                {
                    _stops.Add(stop);
                }
                _logger.LogInformation($"Generated {_stops.Count} stops");

                // Signal that stop generation is complete
                _initializationComplete.Signal();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in stop generation thread");
                _cts.Cancel(); // Cancel overall operation on error
            }
        }

        /// <summary>
        /// Thread procedure for generating vehicles
        /// </summary>
        private void GenerateVehiclesThreadProc()
        {
            try
            {
                // Wait for routes to be available before generating vehicles
                while (_routes.IsEmpty && !_cts.IsCancellationRequested)
                {
                    Thread.Sleep(100);
                }

                if (_cts.IsCancellationRequested)
                {
                    return;
                }

                _logger.LogInformation("Starting vehicle generation thread");
                var vehicles = GenerateVehicles(_routes, Configuration.AverageVehiclesPerRoute);
                foreach (var vehicle in vehicles)
                {
                    _vehicles.Add(vehicle);
                }
                _logger.LogInformation($"Generated {_vehicles.Count} vehicles");

                // Signal that vehicle generation is complete
                _initializationComplete.Signal();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in vehicle generation thread");
                _cts.Cancel(); // Cancel overall operation on error
            }
        }

        /// <summary>
        /// Thread procedure for generating schedules
        /// </summary>
        private void GenerateSchedulesThreadProc()
        {
            _logger.LogInformation("Schedule generation started");

            try
            {
                // Wait until routes and vehicles are generated
                _coordinator.WaitForSignal("RoutesAndVehiclesGenerated");

                // Generate schedules for each route and vehicle
                var schedules = GenerateSchedules(_routes, _stops);

                foreach (var schedule in schedules)
                {
                    _schedules.Add(schedule);
                }

                _logger.LogInformation($"Generated {_schedules.Count} schedules");

                // Signal completion
                _initializationComplete.Signal();
            }
            catch (ThreadInterruptedException)
            {
                _logger.LogInformation("Schedule generation was interrupted");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during schedule generation");
                throw;
            }
        }

        #endregion

        #region Public Generation Methods

        /// <summary>
        /// Generates a fixed set of stops for the given routes
        /// </summary>
        /// <param name="routes">Routes to generate stops for</param>
        /// <param name="averageStopsPerRoute">Average number of stops per route</param>
        /// <returns>A collection of generated stops</returns>
        public IEnumerable<Stop> GenerateStops(IEnumerable<Route> routes, int averageStopsPerRoute)
        {
            _logger.LogInformation($"Generating stops for {routes.Count()} routes with ~{averageStopsPerRoute} stops per route");

            var allStops = new List<Stop>();
            var existingStopNames = new HashSet<string>();
            var routeArray = routes.ToArray();

            foreach (var route in routeArray)
            {
                // Determine number of stops for this route with some randomization
                int stopsForThisRoute = (int)(averageStopsPerRoute * (0.8 + 0.4 * _random.NextDouble()));

                // Create stops for this route
                var routeStops = CreateStopsForRoute(route, stopsForThisRoute, existingStopNames);
                allStops.AddRange(routeStops);

                // Create RouteStop relationships (deferred - when both Route and Stop lists are complete)
            }

            // Ensure we have a reasonable number of common stops between routes
            int desiredCommonStops = routeArray.Length / 3; // About 1/3 of routes should share stops
            if (desiredCommonStops > 2)
            {
                CreateCommonStopsBetweenRoutes(routeArray, allStops, desiredCommonStops);
            }

            return allStops;
        }

        /// <summary>
        /// Creates stops for a single route with proper spacing
        /// </summary>
        private List<Stop> CreateStopsForRoute(Route route, int stopCount, HashSet<string> existingStopNames)
        {
            var stops = new List<Stop>();
            var stopTypes = Enum.GetValues(typeof(StopType)).Cast<StopType>().ToList();

            // Find appropriate stop type for this route
            StopType stopType;
            switch (route.TransportType)
            {
                case VehicleType.Bus: stopType = StopType.BusStop; break;
                case VehicleType.Train: stopType = StopType.TrainStation; break;
                case VehicleType.Tram: stopType = StopType.TramStop; break;
                case VehicleType.Metro: stopType = StopType.MetroStation; break;
                case VehicleType.Ferry: stopType = StopType.FerryTerminal; break;
                case VehicleType.Taxi: stopType = StopType.TaxiStand; break;
                default: stopType = StopType.Other; break;
            }

            // Generate stops along a path
            // Starting from a point near the center, create a path with some random variation
            double startLatitude = Configuration.CenterLatitude + (_random.NextDouble() - 0.5) * (Configuration.RadiusKm / 50.0);
            double startLongitude = Configuration.CenterLongitude + (_random.NextDouble() - 0.5) * (Configuration.RadiusKm / 50.0);

            // Choose a random direction for the route (in radians)
            double routeAngle = _random.NextDouble() * 2.0 * Math.PI;

            // Create stops with spacing along the route
            for (int i = 0; i < stopCount; i++)
            {
                // Calculate position along route with some randomness
                double progress = (double)i / (stopCount - 1); // 0.0 to 1.0 progression along route
                double distance = progress * Configuration.RadiusKm;
                double angle = routeAngle + (_random.NextDouble() - 0.5) * Math.PI / 6; // Some variation in angle

                // Convert polar to cartesian coordinates and then to lat/long
                double dx = distance * Math.Cos(angle);
                double dy = distance * Math.Sin(angle);

                // Convert dx/dy to lat/long (approximate conversion for moderate distances)
                // 1 degree latitude is ~111km, 1 degree longitude varies with latitude
                double lat = startLatitude + (dy / 111.0);
                double lon = startLongitude + (dx / (111.0 * Math.Cos(startLatitude * Math.PI / 180.0)));

                // Generate a unique stop name
                string stopName;
                do
                {
                    stopName = $"{_stopNamePrefixes[_random.Next(_stopNamePrefixes.Length)]} {_stopNameSuffixes[_random.Next(_stopNameSuffixes.Length)]}";
                } while (existingStopNames.Contains(stopName));
                existingStopNames.Add(stopName);

                // Create the stop
                var stop = new Stop
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = stopName,
                    Description = $"Stop for {route.Name}",
                    Latitude = lat,
                    Longitude = lon,
                    Type = stopType,
                    Zone = $"Zone {_random.Next(1, 5)}", // Random zone number
                    Address = $"{_random.Next(1, 200)} {_stopNameSuffixes[_random.Next(_stopNameSuffixes.Length)]}",
                    HasShelter = _random.NextDouble() > 0.3, // 70% chance of shelter
                    HasSeating = _random.NextDouble() > 0.2, // 80% chance of seating
                    IsAccessible = _random.NextDouble() > 0.1, // 90% chance of accessibility
                    HasRealtimeInfo = _random.NextDouble() > 0.5, // 50% chance of realtime info
                    LastUpdated = DateTime.Now
                };

                stops.Add(stop);
            }

            return stops;
        }

        /// <summary>
        /// Creates common stops between routes to simulate transit hubs/interchanges
        /// </summary>
        private void CreateCommonStopsBetweenRoutes(Route[] routes, List<Stop> allStops, int desiredCommonStops)
        {
            // Create transit hubs (common stops between multiple routes)
            // This implementation is simplified - in a complete implementation, we would
            // need to update the route stops collections as well

            // For now, simply mark some stops as common by updating their descriptions
            var potentialHubs = allStops
                .OrderBy(s => Guid.NewGuid()) // Random shuffle
                .Take(desiredCommonStops)
                .ToList();

            foreach (var hub in potentialHubs)
            {
                hub.Name = $"Transit Hub {hub.Name}";
                hub.Description = $"Major interchange hub serving multiple routes";
                hub.HasRealtimeInfo = true; // Hubs always have real-time info
            }
        }

        /// <summary>
        /// Generates a fixed set of vehicles for the given routes
        /// </summary>
        /// <param name="routes">Routes to generate vehicles for</param>
        /// <param name="averageVehiclesPerRoute">Average number of vehicles per route</param>
        /// <returns>A collection of generated vehicles</returns>
        public IEnumerable<Vehicle> GenerateVehicles(IEnumerable<Route> routes, int averageVehiclesPerRoute)
        {
            _logger.LogInformation($"Generating vehicles for {routes.Count()} routes with ~{averageVehiclesPerRoute} vehicles per route");

            var allVehicles = new List<Vehicle>();
            var routeArray = routes.ToArray();
            var regNumbers = new HashSet<string>(); // Track used registration numbers

            // Generate vehicles for each route
            foreach (var route in routeArray)
            {
                // Determine number of vehicles for this route with some randomization
                // More popular/frequent routes have more vehicles
                int baseCount = route.PeakFrequency.HasValue ?
                    Math.Max(2, (int)(60 / route.PeakFrequency.Value)) : averageVehiclesPerRoute;

                // Add some randomness
                int vehiclesForRoute = Math.Max(1, (int)(baseCount * (0.8 + 0.4 * _random.NextDouble())));

                // Create vehicles for this route
                for (int i = 0; i < vehiclesForRoute; i++)
                {
                    var vehicle = CreateVehicleForRoute(route, regNumbers);
                    allVehicles.Add(vehicle);
                }
            }

            return allVehicles;
        }

        /// <summary>
        /// Creates a vehicle for a specific route
        /// </summary>
        /// <param name="route">The route the vehicle serves</param>
        /// <param name="usedRegistrations">Set of already used registration numbers</param>
        private Vehicle CreateVehicleForRoute(Route route, HashSet<string> usedRegistrations)
        {
            // Generate a unique registration number
            string registrationNumber;
            do
            {
                // Format depends on vehicle type
                if (route.TransportType == VehicleType.Bus)
                {
                    registrationNumber = $"BUS-{_random.Next(1000, 9999)}";
                }
                else if (route.TransportType == VehicleType.Train)
                {
                    registrationNumber = $"TR-{_random.Next(100, 999)}";
                }
                else if (route.TransportType == VehicleType.Tram)
                {
                    registrationNumber = $"TM-{_random.Next(1, 99)}";
                }
                else if (route.TransportType == VehicleType.Metro)
                {
                    registrationNumber = $"M-{_random.Next(1, 50)}";
                }
                else
                {
                    registrationNumber = $"{route.TransportType.ToString().Substring(0, 1)}-{_random.Next(1000, 9999)}";
                }
            } while (usedRegistrations.Contains(registrationNumber));
            usedRegistrations.Add(registrationNumber);

            // Determine capacity based on vehicle type
            int capacity;
            switch (route.TransportType)
            {
                case VehicleType.Bus: capacity = _random.Next(40, 120); break;
                case VehicleType.Train: capacity = _random.Next(200, 800); break;
                case VehicleType.Tram: capacity = _random.Next(80, 200); break;
                case VehicleType.Metro: capacity = _random.Next(150, 600); break;
                case VehicleType.Ferry: capacity = _random.Next(100, 400); break;
                default: capacity = _random.Next(4, 50); break;
            }

            // Place vehicle at a random position along the route (will be updated in simulation)
            // For initial position, just use the center coordinates with some offset
            double latOffset = (_random.NextDouble() - 0.5) * (Configuration.RadiusKm / 50);
            double lonOffset = (_random.NextDouble() - 0.5) * (Configuration.RadiusKm / 50);

            // Generate random bearing (0-360 degrees)
            double bearing = _random.NextDouble() * 360;

            // Generate random speed appropriate for the vehicle type
            double speed;
            switch (route.TransportType)
            {
                case VehicleType.Bus: speed = 20 + _random.NextDouble() * 20; break;  // 20-40 km/h
                case VehicleType.Train: speed = 60 + _random.NextDouble() * 80; break; // 60-140 km/h
                case VehicleType.Tram: speed = 15 + _random.NextDouble() * 25; break; // 15-40 km/h
                case VehicleType.Metro: speed = 30 + _random.NextDouble() * 40; break; // 30-70 km/h
                case VehicleType.Ferry: speed = 15 + _random.NextDouble() * 15; break; // 15-30 km/h
                default: speed = 30 + _random.NextDouble() * 20; break;              // 30-50 km/h
            }

            // Create the vehicle
            var vehicle = new Vehicle
            {
                Id = Guid.NewGuid().ToString(),
                RegistrationNumber = registrationNumber,
                Type = route.TransportType,
                Status = _random.NextDouble() > 0.05 ? VehicleStatus.InService : VehicleStatus.Delayed, // 5% chance of delay
                RouteId = route.Id,
                Latitude = Configuration.CenterLatitude + latOffset,
                Longitude = Configuration.CenterLongitude + lonOffset,
                Bearing = bearing,
                Speed = speed,
                LastUpdated = DateTime.Now,
                Capacity = capacity,
                OccupancyPercentage = _random.Next(0, 100), // Random initial occupancy
                IsAccessible = _random.NextDouble() > 0.2,  // 80% chance of accessibility
                HasWifi = _random.NextDouble() > 0.5        // 50% chance of WiFi
            };

            return vehicle;
        }

        /// <summary>
        /// Generates a fixed set of routes
        /// </summary>
        /// <param name="count">Number of routes to generate</param>
        /// <returns>A collection of generated routes</returns>
        public IEnumerable<Route> GenerateRoutes(int count)
        {
            _logger.LogInformation($"Generating {count} routes");

            var routes = new List<Route>();
            var vehicleTypes = Enum.GetValues(typeof(VehicleType)).Cast<VehicleType>().ToList();

            for (int i = 0; i < count; i++)
            {
                // Choose transport type for this route
                var transportType = vehicleTypes[_random.Next(vehicleTypes.Count)];

                // Generate route number/name
                string routeName;
                if (transportType == VehicleType.Bus || transportType == VehicleType.Tram)
                {
                    // For bus/tram, use numbers
                    routeName = $"{_routeNames[_random.Next(_routeNames.Length)]} {_random.Next(1, 100)}";
                }
                else
                {
                    // For other types, use colored lines
                    var colors = new[] { "Red", "Blue", "Green", "Yellow", "Orange", "Purple", "Brown", "Gray", "Pink" };
                    routeName = $"{colors[_random.Next(colors.Length)]} {_routeNames[_random.Next(_routeNames.Length)]}";
                }

                // Generate origin and destination
                string origin = $"{_stopNamePrefixes[_random.Next(_stopNamePrefixes.Length)]} {_stopNameSuffixes[_random.Next(_stopNameSuffixes.Length)]}";
                string destination;
                do
                {
                    destination = $"{_stopNamePrefixes[_random.Next(_stopNamePrefixes.Length)]} {_stopNameSuffixes[_random.Next(_stopNameSuffixes.Length)]}";
                } while (destination == origin); // Ensure different origin and destination

                // Generate route color
                string routeColor = _routeColors[_random.Next(_routeColors.Length)];
                string textColor = IsColorDark(routeColor) ? "#FFFFFF" : "#000000";

                var route = new Route
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = routeName,
                    Description = $"{routeName} from {origin} to {destination}",
                    TransportType = transportType,
                    Color = routeColor,
                    TextColor = textColor,
                    Origin = origin,
                    Destination = destination,
                    IsActive = true,
                    PeakFrequency = _random.Next(5, 20),
                    OffPeakFrequency = _random.Next(15, 40),
                    AverageJourneyTime = _random.Next(20, 120)
                };

                routes.Add(route);
            }

            return routes;
        }

        /// <summary>
        /// Determines if a color (in hex format) is dark
        /// </summary>
        /// <param name="hexColor">Color in hex format (#RRGGBB)</param>
        /// <returns>True if the color is dark, false otherwise</returns>
        private bool IsColorDark(string hexColor)
        {
            if (hexColor.StartsWith("#"))
            {
                hexColor = hexColor.Substring(1);
            }

            if (hexColor.Length == 6)
            {
                int r = Convert.ToInt32(hexColor.Substring(0, 2), 16);
                int g = Convert.ToInt32(hexColor.Substring(2, 2), 16);
                int b = Convert.ToInt32(hexColor.Substring(4, 2), 16);

                // Calculate brightness (ITU-R BT.709)
                double brightness = (0.299 * r + 0.587 * g + 0.114 * b) / 255;

                // If brightness is less than 0.5, the color is considered dark
                return brightness < 0.5;
            }

            return false; // Default to not dark if invalid format
        }

        #endregion

        /// <summary>
        /// Checks if a datetime is today
        /// </summary>
        /// <param name="time">The time to check</param>
        /// <returns>True if the date part matches today</returns>
        private bool IsToday(DateTime time)
        {
            return time.Date == DateTime.Now.Date;
        }

        /// <summary>
        /// Main simulation thread procedure that updates vehicle positions and schedules
        /// </summary>
        private void SimulationThreadProc()
        {
            _logger.LogInformation("Simulation thread started");

            try
            {
                // Start simulation time tracking
                _simulationStartTime = DateTime.Now;
                _simulationCurrentTime = _simulationStartTime;
                _simulationStopwatch.Start();

                // Use local copies to prevent race conditions
                var localVehicles = _vehicles.ToList();
                var localSchedules = _schedules.ToList();
                var activeSchedules = localSchedules
                    .Where(s => IsToday(s.DepartureTime) &&
                              s.DepartureTime <= _simulationCurrentTime.AddHours(2) &&
                              s.ArrivalTime >= _simulationCurrentTime)
                    .ToList();

                _logger.LogInformation($"Starting simulation with {localVehicles.Count} vehicles and {activeSchedules.Count} active schedules");

                while (!_cts.Token.IsCancellationRequested)
                {
                    // Update simulation time based on elapsed time and speed factor
                    var elapsed = _simulationStopwatch.Elapsed;
                    _simulationCurrentTime = _simulationStartTime.AddMilliseconds(elapsed.TotalMilliseconds * Configuration.SimulationSpeedFactor);

                    // Update vehicle positions
                    UpdateVehiclePositions(localVehicles, activeSchedules);

                    // Filter out completed schedules and add new ones coming up
                    activeSchedules.RemoveAll(s => s.ArrivalTime < _simulationCurrentTime);

                    var newActiveSchedules = localSchedules
                        .Where(s => IsToday(s.DepartureTime) &&
                                  s.DepartureTime <= _simulationCurrentTime.AddHours(1) &&
                                  s.ArrivalTime >= _simulationCurrentTime &&
                                  !activeSchedules.Any(a => a.Id == s.Id))
                        .ToList();

                    if (newActiveSchedules.Any())
{
    _logger.LogInformation($"Adding {newActiveSchedules.Count} new active schedules");
    activeSchedules.AddRange(newActiveSchedules);
}

// Notify subscribers that data has been updated
OnDataUpdated(new MockDataUpdatedEventArgs(
    routes: _routes.ToList(),
    stops: _stops.ToList(),
    vehicles: localVehicles,
    schedules: activeSchedules,
    timestamp: DateTime.Now,
    simulatedTime: _simulationCurrentTime
));

// Sleep for the configured update interval
Thread.Sleep(Configuration.UpdateIntervalMs);
}
            }
            catch (ThreadInterruptedException)
            {
                _logger.LogInformation("Simulation thread was interrupted");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in simulation thread");
            }
            finally
            {
                _simulationStopwatch.Stop();
                _logger.LogInformation("Simulation thread stopped");
            }
        }

        /// <summary>
        /// Updates vehicle positions based on their schedules and elapsed time
        /// </summary>
        /// <param name="vehicles">List of vehicles to update</param>
        /// <param name="activeSchedules">List of active schedules</param>
        private void UpdateVehiclePositions(List<Vehicle> vehicles, List<Schedule> activeSchedules)
        {
            // Group active schedules by vehicle
            var schedulesByVehicle = activeSchedules
                .Where(s => !string.IsNullOrEmpty(s.VehicleId))
                .GroupBy(s => s.VehicleId)
                .ToDictionary(g => g.Key, g => g.OrderBy(s => s.DepartureTime).First());

            // Group vehicles by route to get route information
            var vehiclesByRoute = vehicles.GroupBy(v => v.RouteId)
                                        .ToDictionary(g => g.Key, g => g.ToList());

            // Maps of routes and stops for lookup
            var routeMap = _routes.ToDictionary(r => r.Id);
            var stopsByRoute = _stops.GroupBy(s => s.RouteId)
                                   .ToDictionary(g => g.Key, g => g.OrderBy(s => s.SequenceNumber).ToList());

            // Update each vehicle with an active schedule
            foreach (var vehicle in vehicles)
            {
                // Skip vehicles without active schedules
                if (!schedulesByVehicle.TryGetValue(vehicle.Id, out var schedule))
                {
                    // For vehicles without active schedules, let's make some of them move randomly
                    if (_random.NextDouble() < 0.1) // 10% chance for random movement
                    {
                        UpdateVehicleRandomMovement(vehicle);
                    }
                    continue;
                }

                // Get route information
                if (!routeMap.TryGetValue(vehicle.RouteId, out var route))
                {
                    continue;
                }

                // Get stops for this route
                if (!stopsByRoute.TryGetValue(vehicle.RouteId, out var routeStops) || routeStops.Count < 2)
                {
                    continue;
                }

                // Calculate progress along route based on schedule times
                double tripProgress = 0;

                // If not yet departed, position at first stop
                if (_simulationCurrentTime < schedule.DepartureTime)
                {
                    var firstStop = routeStops.First();
                    vehicle.Latitude = firstStop.Latitude;
                    vehicle.Longitude = firstStop.Longitude;
                    vehicle.Status = VehicleStatus.Stopped;
                }
                // If already arrived, position at last stop
                else if (_simulationCurrentTime >= schedule.ArrivalTime)
                {
                    var lastStop = routeStops.Last();
                    vehicle.Latitude = lastStop.Latitude;
                    vehicle.Longitude = lastStop.Longitude;
                    vehicle.Status = VehicleStatus.Stopped;
                }
                // In transit - calculate position along route
                else
                {
                    tripProgress = (_simulationCurrentTime - schedule.DepartureTime).TotalMinutes /
                                  (schedule.ArrivalTime - schedule.DepartureTime).TotalMinutes;

                    // Ensure progress is between 0 and 1
                    tripProgress = Math.Max(0, Math.Min(1, tripProgress));

                    // If we're at an exact stop, use that position
                    int stopIndex = (int)(tripProgress * (routeStops.Count - 1));
                    double stopProgress = tripProgress * (routeStops.Count - 1) - stopIndex;

                    // If exactly at a stop
                    if (Math.Abs(stopProgress) < 0.01)
                    {
                        var stop = routeStops[stopIndex];
                        vehicle.Latitude = stop.Latitude;
                        vehicle.Longitude = stop.Longitude;
                        vehicle.Status = VehicleStatus.Stopped;
                        vehicle.Speed = 0;
                    }
                    // In between stops
                    else
                    {
                        var currentStop = routeStops[stopIndex];
                        var nextStop = routeStops[stopIndex + 1];

                        // Linear interpolation between the two stops
                        vehicle.Latitude = currentStop.Latitude + stopProgress * (nextStop.Latitude - currentStop.Latitude);
                        vehicle.Longitude = currentStop.Longitude + stopProgress * (nextStop.Longitude - currentStop.Longitude);

                        // Calculate bearing between the two stops
                        vehicle.Bearing = CalculateBearing(
                            currentStop.Latitude, currentStop.Longitude,
                            nextStop.Latitude, nextStop.Longitude);

                        // Calculate speed based on distance and time between stops
                        double distanceKm = CalculateDistance(
                            currentStop.Latitude, currentStop.Longitude,
                            nextStop.Latitude, nextStop.Longitude);

                        double timeMinutes = (schedule.ArrivalTime - schedule.DepartureTime).TotalMinutes / (routeStops.Count - 1);
                        double speedKmh = distanceKm / (timeMinutes / 60);

                        vehicle.Status = VehicleStatus.InTransit;
                        vehicle.Speed = speedKmh;

                        // Set occupancy higher during peak hours
                        if (schedule.IsPeakHour)
                        {
                            vehicle.OccupancyPercentage = _random.Next(60, 101); // 60-100%
                        }
                        else
                        {
                            vehicle.OccupancyPercentage = _random.Next(20, 81); // 20-80%
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Updates a vehicle with random movement for vehicles without active schedules
        /// </summary>
        private void UpdateVehicleRandomMovement(Vehicle vehicle)
        {
            // Small random movement
            double latChange = (_random.NextDouble() - 0.5) * 0.001; // ~100m
            double lonChange = (_random.NextDouble() - 0.5) * 0.001;

            vehicle.Latitude += latChange;
            vehicle.Longitude += lonChange;
            vehicle.Bearing = _random.Next(360); // Random direction
            vehicle.Speed = _random.Next(5, 50); // Random speed 5-50 km/h
            vehicle.Status = _random.NextDouble() < 0.2 ? VehicleStatus.Stopped : VehicleStatus.InTransit;
            vehicle.OccupancyPercentage = _random.Next(100);
        }

        /// <summary>
        /// Calculate distance between two points using Haversine formula
        /// </summary>
        /// <returns>Distance in kilometers</returns>
        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double earthRadius = 6371; // km
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return earthRadius * c;
        }

        /// <summary>
        /// Calculate bearing between two points
        /// </summary>
        /// <returns>Bearing in degrees (0-360)</returns>
        private double CalculateBearing(double lat1, double lon1, double lat2, double lon2)
        {
            var dLon = ToRadians(lon2 - lon1);
            var lat1Rad = ToRadians(lat1);
            var lat2Rad = ToRadians(lat2);

            var y = Math.Sin(dLon) * Math.Cos(lat2Rad);
            var x = Math.Cos(lat1Rad) * Math.Sin(lat2Rad) -
                    Math.Sin(lat1Rad) * Math.Cos(lat2Rad) * Math.Cos(dLon);

            var bearing = Math.Atan2(y, x);
            bearing = ToDegrees(bearing);
            bearing = (bearing + 360) % 360;

            return bearing;
        }

        /// <summary>
        /// Convert degrees to radians
        /// </summary>
        private double ToRadians(double degrees)
        {
            return degrees * Math.PI / 180;
        }

        /// <summary>
        /// Convert radians to degrees
        /// </summary>
        private double ToDegrees(double radians)
        {
            return radians * 180 / Math.PI;
        }

        /// <summary>
        /// Applies random variation to a base value
        /// </summary>
        private int ApplyRandomVariation(int baseValue, double variationPercentage = 0.3)
        {
            double variation = ((_random.NextDouble() * 2) - 1) * variationPercentage;
            return Math.Max(1, (int)(baseValue * (1 + variation)));
        }

        /// <summary>
        /// Raises the DataUpdated event
        /// </summary>
        private void OnDataUpdated(MockDataUpdatedEventArgs args)
        {
            DataUpdated?.Invoke(this, args);
        }
        
        /// <summary>
        /// Generates a set of schedules for the given routes and stops
        /// </summary>
        /// <param name="routes">Routes to generate schedules for</param>
        /// <param name="stops">Stops to include in schedules</param>
        /// <returns>A collection of generated schedules</returns>
        public IEnumerable<Schedule> GenerateSchedules(IEnumerable<Route> routes, IEnumerable<Stop> stops)
        {
            _logger.LogInformation($"Generating schedules for {routes?.Count() ?? 0} routes with {stops?.Count() ?? 0} stops");
            var schedules = new List<Schedule>();
            var routesArray = routes?.ToArray() ?? Array.Empty<Route>();
            var stopsArray = stops?.ToArray() ?? Array.Empty<Stop>();
            
            if (routesArray.Length == 0 || stopsArray.Length == 0)
            {
                _logger.LogWarning("Cannot generate schedules: no routes or stops provided");
                return schedules;
            }

            foreach (var route in routesArray)
            {
                // Find stops for this route
                var routeStops = stopsArray.Where(s => s.RouteId == route.Id).ToList();
                if (routeStops.Count < 2)
                {
                    _logger.LogWarning($"Route {route.Id} has fewer than 2 stops, skipping schedule generation");
                    continue;
                }

                // Sort stops by their sequence in the route
                routeStops = routeStops.OrderBy(s => s.SequenceNumber).ToList();

                // Generate schedules for different times of day
                var startTimes = GenerateStartTimes(Configuration.ScheduleStartTimeHour, Configuration.ScheduleEndTimeHour,
                    Configuration.AverageTripFrequencyMinutes);

                foreach (var startTime in startTimes)
                {
                    var schedule = new Schedule
                    {
                        Id = Guid.NewGuid().ToString(),
                        RouteId = route.Id,
                        StartStopId = routeStops.First().Id,
                        EndStopId = routeStops.Last().Id,
                        DepartureTime = startTime,
                        EstimatedArrivalTime = startTime.AddMinutes(routeStops.Count * 3), // Approx. 3 min per stop
                        IsPeakHour = IsPeakHour(startTime),
                        IsActive = IsScheduleActive(startTime, DateTime.Now)
                    };

                    schedules.Add(schedule);
                }
            }

            _logger.LogInformation($"Generated {schedules.Count} schedules");
            return schedules;
        }

        /// <summary>
        /// Generate departure times for schedules throughout the day
        /// </summary>
        private List<DateTime> GenerateStartTimes(int startHour, int endHour, int frequencyMinutes)
        {
            var times = new List<DateTime>();
            var today = DateTime.Today;

            for (int hour = startHour; hour <= endHour; hour++)
            {
                // Adjust frequency during peak hours
                int adjustedFrequency = IsPeakHour(new DateTime(today.Year, today.Month, today.Day, hour, 0, 0))
                    ? (int)(frequencyMinutes * 0.7) // More frequent during peak hours
                    : frequencyMinutes;

                // Ensure at least 5 minute frequency
                adjustedFrequency = Math.Max(adjustedFrequency, 5);

                for (int minute = 0; minute < 60; minute += adjustedFrequency)
                {
                    times.Add(new DateTime(today.Year, today.Month, today.Day, hour, minute, 0));
                }
            }

            return times;
        }

        /// <summary>
        /// Check if a time is during peak hours (7-9 AM or 4-7 PM)
        /// </summary>
        private bool IsPeakHour(DateTime time)
        {
            int hour = time.Hour;
            return (hour >= 7 && hour <= 9) || (hour >= 16 && hour <= 19);
        }

        /// <summary>
        /// Determine if a schedule is active based on its departure time
        /// </summary>
        private bool IsScheduleActive(DateTime departureTime, DateTime currentTime)
        {
            // If departure is in the past but within the last 2 hours, it's still active
            // If departure is in the future but within the next 2 hours, it's active
            var timeDiff = (departureTime - currentTime).TotalMinutes;
            return timeDiff >= -120 && timeDiff <= 120;
        }

        /// <summary>
        /// Generates the next batch of vehicle positions based on current state and time
        /// </summary>
        /// <param name="vehicles">Current vehicles to update</param>
        /// <param name="elapsedSeconds">Seconds elapsed since last update</param>
        /// <returns>Updated collection of vehicles with new positions</returns>
        public IEnumerable<Vehicle> UpdateVehiclePositions(IEnumerable<Vehicle> vehicles, double elapsedSeconds)
        {
            if (vehicles == null)
                return Array.Empty<Vehicle>();
                
            var vehicleList = vehicles.ToList();
            var scheduleList = _schedules?.ToList() ?? new List<Schedule>();
            
            // Get active schedules
            var activeSchedules = scheduleList
                .Where(s => s.IsActive)
                .ToList();
                
            foreach (var vehicle in vehicleList)
            {
                // Find schedule for this vehicle
                var schedule = activeSchedules
                    .FirstOrDefault(s => s.VehicleId == vehicle.Id);
                
                if (schedule != null)
                {
                    // Update vehicle based on its schedule
                    UpdateVehicleBasedOnSchedule(vehicle, schedule, elapsedSeconds);
                }
                else
                {
                    // Random movement for vehicles without active schedules
                    UpdateVehicleRandomMovement(vehicle);
                }
            }
            
            return vehicleList;
        }

        /// <summary>
        /// Update a vehicle position based on its schedule
        /// </summary>
        private void UpdateVehicleBasedOnSchedule(Vehicle vehicle, Schedule schedule, double elapsedSeconds)
        {
            // Get route and stops for this schedule
            var route = _routes.FirstOrDefault(r => r.Id == schedule.RouteId);
            if (route == null) return;
            
            var stops = _stops
                .Where(s => s.RouteId == route.Id)
                .OrderBy(s => s.SequenceNumber)
                .ToList();
                
            if (stops.Count < 2) return;
            
            // Determine vehicle progress along route
            double totalJourneyTime = (schedule.EstimatedArrivalTime - schedule.DepartureTime).TotalSeconds;
            double elapsedJourneyTime = (DateTime.Now - schedule.DepartureTime).TotalSeconds;
            
            if (elapsedJourneyTime < 0)
            {
                // Vehicle waiting at first stop
                vehicle.Latitude = stops[0].Latitude;
                vehicle.Longitude = stops[0].Longitude;
                vehicle.Status = VehicleStatus.Stopped;
                vehicle.Speed = 0;
                return;
            }
            
            if (elapsedJourneyTime > totalJourneyTime)
            {
                // Vehicle completed journey, put at last stop
                vehicle.Latitude = stops[^1].Latitude;
                vehicle.Longitude = stops[^1].Longitude;
                vehicle.Status = VehicleStatus.Stopped;
                vehicle.Speed = 0;
                return;
            }
            
            // Vehicle is in transit between stops
            double routeProgress = elapsedJourneyTime / totalJourneyTime;
            int numSegments = stops.Count - 1;
            int currentSegment = (int)(routeProgress * numSegments);
            
            if (currentSegment >= numSegments) currentSegment = numSegments - 1;
            
            Stop fromStop = stops[currentSegment];
            Stop toStop = stops[currentSegment + 1];
            
            // Calculate progress within this segment
            double segmentSize = 1.0 / numSegments;
            double segmentProgress = (routeProgress - (currentSegment * segmentSize)) / segmentSize;
            
            // Interpolate position between stops
            vehicle.Latitude = fromStop.Latitude + (toStop.Latitude - fromStop.Latitude) * segmentProgress;
            vehicle.Longitude = fromStop.Longitude + (toStop.Longitude - fromStop.Longitude) * segmentProgress;
            
            // Calculate speed and bearing
            double distanceKm = CalculateDistance(fromStop.Latitude, fromStop.Longitude, 
                                                toStop.Latitude, toStop.Longitude);
            double timeMinutes = (segmentSize * totalJourneyTime) / 60;
            double speedKmh = distanceKm / (timeMinutes / 60);
            
            vehicle.Speed = speedKmh;
            vehicle.Bearing = CalculateBearing(fromStop.Latitude, fromStop.Longitude, 
                                            toStop.Latitude, toStop.Longitude);
            vehicle.Status = VehicleStatus.InTransit;
            
            // Set occupancy higher during peak hours
            if (IsPeakHour(DateTime.Now))
            {
                vehicle.OccupancyPercentage = _random.Next(60, 101); // 60-100%
            }
            else
            {
                vehicle.OccupancyPercentage = _random.Next(20, 81); // 20-80%
            }
        }
        
        /// <summary>
        /// Releases all resources used by the MockDataGenerator
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        /// <summary>
        /// Releases the unmanaged resources used by the MockDataGenerator and optionally releases the managed resources
        /// </summary>
        /// <param name="disposing">True to release both managed and unmanaged resources; false to release only unmanaged resources</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;
                
            if (disposing)
            {
                // Stop any running processes
                if (_isRunning)
                {
                    try
                    {
                        StopAsync().GetAwaiter().GetResult();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error stopping mock data generator during disposal");
                    }
                }
                
                // Dispose managed resources
                _cts?.Dispose();
                // _dataLock?.Dispose(); // AsyncLock does not have Dispose()
                _coordinator?.Dispose();
                _initializationComplete?.Dispose();
            }
            
            // Free unmanaged resources
            
            _disposed = true;
        }
    }
}
