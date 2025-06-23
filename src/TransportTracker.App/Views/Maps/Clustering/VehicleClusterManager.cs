using MauiMap = Microsoft.Maui.Controls.Maps.Map;
using Microsoft.Maui.Devices.Sensors;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using TransportTracker.App.Views.Maps.Overlays;

namespace TransportTracker.App.Views.Maps.Clustering
{
    /// <summary>
    /// Manages clustering of transport vehicles on the map to reduce visual clutter in busy areas
    /// </summary>
    public class VehicleClusterManager
    {
        // Cluster threshold distance in meters
        private readonly double _clusterDistanceThreshold;
        
        // Minimum vehicles required to form a cluster
        private readonly int _minClusterSize;
        
        // The map the clusters are displayed on
        private readonly MauiMap _map;
        
        // Collection of all vehicles being tracked
        private readonly ObservableCollection<TransportVehicle> _vehicles; // Changed from IEnumerable to ObservableCollection for mutability
        
        // Collection of active clusters
        private readonly List<VehicleCluster> _clusters = new();
        
        // Vehicles that are individually displayed (not clustered)
        private readonly List<TransportVehicle> _unclustered = new();

        /// <summary>
        /// Gets the collection of active clusters
        /// </summary>
        public IReadOnlyList<VehicleCluster> Clusters => _clusters;
        
        /// <summary>
        /// Gets the collection of unclustered vehicles
        /// </summary>
        public IReadOnlyList<TransportVehicle> UncluseredVehicles => _unclustered;
        
        /// <summary>
        /// Gets or sets the distance threshold in meters for clustering vehicles
        /// </summary>
        public double ClusterDistanceThreshold
        {
            get => _clusterDistanceThreshold;
            init => _clusterDistanceThreshold = value;
        }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="VehicleClusterManager"/> class
        /// </summary>
        /// <param name="map">The map to add clusters to</param>
        /// <param name="vehicles">Collection of vehicles to manage</param>
        /// <param name="clusterDistance">Distance threshold in meters for creating clusters</param>
        /// <param name="minClusterSize">Minimum number of vehicles required to form a cluster</param>
        public VehicleClusterManager(MauiMap map, ObservableCollection<TransportVehicle> vehicles, double clusterDistance = 150, int minClusterSize = 3)
        {
            _map = map;
            _vehicles = vehicles;
            _clusterDistanceThreshold = clusterDistance;
            _minClusterSize = minClusterSize;
            _vehicles.CollectionChanged += OnVehiclesCollectionChanged;
        }
        
        /// <summary>
        /// Updates the clusters based on current vehicle positions
        /// </summary>
        public void UpdateClusters()
        {
            UpdateClusters(_vehicles);
        }
        
        /// <summary>
        /// Updates the clusters with a new collection of vehicles
        /// </summary>
        public void UpdateClusters(IEnumerable<TransportVehicle> vehicles)
        {
            _clusters.Clear();
            
            // Update the vehicles collection
            _vehicles.Clear();
            if (vehicles != null)
            {
                foreach (var vehicle in vehicles)
                {
                    _vehicles.Add(vehicle);
                }
            }
            
            // Skip clustering if there are no vehicles
            if (_vehicles.Count == 0)
                return;
                
            // Process each vehicle
            var remainingVehicles = new List<TransportVehicle>(_vehicles);
            
            while (remainingVehicles.Count > 0)
            {
                // Take first vehicle as potential cluster center
                var currentVehicle = remainingVehicles[0];
                remainingVehicles.RemoveAt(0);
                
                // Find nearby vehicles within clustering radius
                var nearbyVehicles = FindVehiclesWithinDistance(_vehicles.ToArray(), currentVehicle, _clusterDistanceThreshold);
                
                // Remove nearby vehicles from the remaining list
                foreach (var vehicle in nearbyVehicles)
                {
                    remainingVehicles.Remove(vehicle);
                }
                
                // Create a cluster if there are nearby vehicles or a single vehicle outside clustering distance
                var allClusterVehicles = new List<TransportVehicle> { currentVehicle };
                allClusterVehicles.AddRange(nearbyVehicles);
                
                if (allClusterVehicles.Count > 1 || allClusterVehicles.Count == 1)
                {
                    var clusterCenter = CalculateClusterCenter(allClusterVehicles);
                    var vehicleIds = allClusterVehicles.Select(v => v.Id).ToList();
                    var vehicleTypes = new HashSet<string>(allClusterVehicles.Select(v => v.Type));
                    
                    var cluster = new VehicleCluster(clusterCenter, vehicleIds, vehicleTypes);
                    _clusters.Add(cluster);
                }
                else
                {
                    // This vehicle doesn't form a cluster
                    _unclustered.Add(vehicle);
                    processedVehicles.Add(vehicle.Id);
                }
            }
        }
        
        /// <summary>
        /// Applies the current clustering to the map
        /// </summary>
        /// <param name="clearExisting">Whether to clear existing map elements</param>
        public void ApplyClustersToMauiMap(bool clearExisting = true)
        {
            // Only clear vehicle and cluster pins if requested
            if (clearExisting)
            {
                // Find and remove existing pins
                var existingPins = _map.Pins.Where(p => p is VehicleCluster || p is TransportVehicle).ToList();
                foreach (var pin in existingPins)
                {
                    _map.Pins.Remove(pin);
                }
            }
            
            // Add clusters to map
            foreach (var cluster in _clusters)
            {
                _map.Pins.Add(cluster);
            }
            
            // Add unclustered vehicles to map
            foreach (var vehicle in _unclustered)
            {
                _map.Pins.Add(vehicle);
            }
        }
        
        /// <summary>
        /// Expands a cluster to show individual vehicles
        /// </summary>
        /// <param name="cluster">The cluster to expand</param>
        /// <returns>List of vehicle pins that were expanded from the cluster</returns>
        public List<TransportVehicle> ExpandCluster(VehicleCluster cluster)
        {
            if (cluster == null || !_clusters.Contains(cluster))
                return new List<TransportVehicle>();
                
            // Remove the cluster
            _clusters.Remove(cluster);
            _map.Pins.Remove(cluster);
            
            // Find the vehicles in this cluster
            var expandedVehicles = _vehicles
                .Where(v => cluster.VehicleIds.Contains(v.Id))
                .ToList();
                
            // Add these vehicles to the unclustered collection
            _unclustered.AddRange(expandedVehicles);
            
            // Add the individual vehicles to the map
            foreach (var vehicle in expandedVehicles)
            {
                _map.Pins.Add(vehicle);
            }
            
            return expandedVehicles;
        }
        
        /// <summary>
        /// Finds all vehicles within a specified distance of a reference vehicle
        /// </summary>
        /// <param name="allVehicles">Collection of all vehicles</param>
        /// <param name="referenceVehicle">The vehicle to measure from</param>
        /// <param name="distance">Maximum distance in meters</param>
        /// <returns>List of vehicles within the distance threshold</returns>
        private List<TransportVehicle> FindVehiclesWithinDistance(
            TransportVehicle[] allVehicles, 
            TransportVehicle referenceVehicle, 
            double distance)
        {
            var result = new List<TransportVehicle>();
            var refLocation = referenceVehicle.Location;
            
            foreach (var vehicle in allVehicles)
            {
                if (Distance(refLocation, vehicle.Location) <= distance)
                {
                    result.Add(vehicle);
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Calculates the center point of a collection of vehicles
        /// </summary>
        /// <param name="vehicles">Collection of vehicles</param>
        /// <returns>Center location</returns>
        private Location CalculateClusterCenter(List<TransportVehicle> vehicles)
        {
            if (vehicles == null || !vehicles.Any())
                throw new ArgumentException("Cannot calculate center for empty collection");
                
            double totalLat = 0;
            double totalLon = 0;
            
            foreach (var vehicle in vehicles)
            {
                totalLat += vehicle.Location.Latitude;
                totalLon += vehicle.Location.Longitude;
            }
            
            return new Location(totalLat / vehicles.Count, totalLon / vehicles.Count);
        }
        
        /// <summary>
        /// Calculates the distance between two geographic locations
        /// </summary>
        /// <param name="loc1">First location</param>
        /// <param name="loc2">Second location</param>
        /// <returns>Distance in meters</returns>
        private double Distance(Location loc1, Location loc2)
        {
            // Implementation of the Haversine formula for calculating distance between coordinates
            const double earthRadius = 6371000; // Earth radius in meters
            
            var lat1Rad = DegreesToRadians(loc1.Latitude);
            var lat2Rad = DegreesToRadians(loc2.Latitude);
            var deltaLat = DegreesToRadians(loc2.Latitude - loc1.Latitude);
            var deltaLon = DegreesToRadians(loc2.Longitude - loc1.Longitude);
            
            var a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) +
                   Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                   Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            
            return earthRadius * c;
        }
        
        /// <summary>
        /// Converts degrees to radians
        /// </summary>
        /// <param name="degrees">Angle in degrees</param>
        /// <returns>Angle in radians</returns>
        private double DegreesToRadians(double degrees) => degrees * Math.PI / 180;
        
        /// <summary>
        /// Handles collection changed events for the vehicles collection
        /// </summary>
        private void OnVehiclesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // Update clusters when vehicles collection changes
            UpdateClusters();
            ApplyClustersToMauiMap();
        }
    }
}
