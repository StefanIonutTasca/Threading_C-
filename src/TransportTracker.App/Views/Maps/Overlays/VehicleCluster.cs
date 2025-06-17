using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Graphics;

namespace TransportTracker.App.Views.Maps.Overlays
{
    /// <summary>
    /// Represents a cluster of transport vehicles on the map for areas with many vehicles
    /// </summary>
    public class VehicleCluster : Pin
    {
        /// <summary>
        /// Gets or sets the collection of vehicle IDs contained in this cluster
        /// </summary>
        public List<string> VehicleIds { get; set; } = new List<string>();
        
        /// <summary>
        /// Gets or sets the count of vehicles in this cluster
        /// </summary>
        public int Count => VehicleIds.Count;
        
        /// <summary>
        /// Gets the center location of the cluster
        /// </summary>
        public Location ClusterCenter => Location;
        
        /// <summary>
        /// Gets or sets the radius of the cluster in meters
        /// </summary>
        public double ClusterRadius { get; set; }
        
        /// <summary>
        /// Gets or sets the color of the cluster
        /// </summary>
        public Color ClusterColor { get; set; } = Colors.Red;
        
        /// <summary>
        /// Gets or sets whether the cluster is expandable on click
        /// </summary>
        public bool IsExpandable { get; set; } = true;
        
        /// <summary>
        /// Gets or sets the vehicle types contained in this cluster
        /// </summary>
        public HashSet<string> VehicleTypes { get; set; } = new HashSet<string>();

        /// <summary>
        /// Initializes a new instance of the <see cref="VehicleCluster"/> class
        /// </summary>
        public VehicleCluster()
        {
            // Set default pin style for clusters
            Label = "Multiple Vehicles";
            ImageSource = "cluster_icon.png";
            IsDraggable = false;
        }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="VehicleCluster"/> class with parameters
        /// </summary>
        /// <param name="center">The center location of the cluster</param>
        /// <param name="vehicleIds">The IDs of vehicles in this cluster</param>
        /// <param name="vehicleTypes">The types of vehicles in this cluster</param>
        public VehicleCluster(Location center, List<string> vehicleIds, HashSet<string> vehicleTypes = null)
        {
            Location = center;
            VehicleIds = vehicleIds;
            VehicleTypes = vehicleTypes ?? new HashSet<string>();
            
            // Calculate cluster radius based on vehicle count
            ClusterRadius = Math.Min(100 + (Count * 5), 500);
            
            // Set appearance based on cluster size
            Label = $"{Count} Vehicles";
            ImageSource = "cluster_icon.png";
            IsDraggable = false;
            
            UpdateClusterAppearance();
        }
        
        /// <summary>
        /// Adds a vehicle to the cluster
        /// </summary>
        /// <param name="vehicleId">ID of the vehicle to add</param>
        /// <param name="vehicleType">Type of the vehicle to add</param>
        /// <returns>True if the vehicle was added, false if it was already in the cluster</returns>
        public bool AddVehicle(string vehicleId, string vehicleType = null)
        {
            if (VehicleIds.Contains(vehicleId))
                return false;
                
            VehicleIds.Add(vehicleId);
            
            if (!string.IsNullOrEmpty(vehicleType))
                VehicleTypes.Add(vehicleType);
                
            UpdateClusterAppearance();
            return true;
        }
        
        /// <summary>
        /// Removes a vehicle from the cluster
        /// </summary>
        /// <param name="vehicleId">ID of the vehicle to remove</param>
        /// <returns>True if the vehicle was removed, false if it wasn't in the cluster</returns>
        public bool RemoveVehicle(string vehicleId)
        {
            if (!VehicleIds.Contains(vehicleId))
                return false;
                
            VehicleIds.Remove(vehicleId);
            UpdateClusterAppearance();
            return true;
        }
        
        /// <summary>
        /// Updates the appearance of the cluster based on the current vehicle count and types
        /// </summary>
        private void UpdateClusterAppearance()
        {
            // Update label to show current count
            Label = Count == 1 ? "1 Vehicle" : $"{Count} Vehicles";
            
            // Update description to show vehicle types
            if (VehicleTypes.Count > 0)
            {
                Address = string.Join(", ", VehicleTypes);
            }
            
            // Adjust cluster radius based on vehicle count
            ClusterRadius = Math.Min(100 + (Count * 5), 500);
            
            // Set color based on dominant vehicle type if available
            if (VehicleTypes.Count > 0)
            {
                var dominantType = VehicleTypes.GroupBy(t => t)
                    .OrderByDescending(g => g.Count())
                    .Select(g => g.Key)
                    .FirstOrDefault();
                    
                switch (dominantType?.ToLower())
                {
                    case "bus": 
                        ClusterColor = Color.FromRgba(0, 120, 215, 200); // Blue
                        break;
                    case "tram": 
                        ClusterColor = Color.FromRgba(0, 153, 0, 200); // Green
                        break;
                    case "subway": 
                        ClusterColor = Color.FromRgba(230, 0, 0, 200); // Red
                        break;
                    case "train": 
                        ClusterColor = Color.FromRgba(209, 52, 56, 200); // Orange
                        break;
                    default: 
                        ClusterColor = Color.FromRgba(135, 100, 184, 200); // Purple
                        break;
                }
            }
        }
    }
}
