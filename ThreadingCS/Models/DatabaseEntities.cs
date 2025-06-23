using System;
using System.Collections.Generic;
using SQLite;

namespace ThreadingCS.Models
{
    [Table("TransportRouteEntity")]
    public class TransportRouteEntity
    {
        [PrimaryKey]
        public string RouteId { get; set; }
        public string RouteName { get; set; }
        public string AgencyName { get; set; }
        public string Color { get; set; }
        public double Duration { get; set; }
        public double Distance { get; set; }
        public DateTime SavedAt { get; set; }
    }

    [Table("TransportStopEntity")]
    public class TransportStopEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string StopId { get; set; }
        public string StopName { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public DateTime EstimatedArrival { get; set; }
        
        // Foreign key
        [Indexed]
        public string RouteId { get; set; }
    }

    [Table("VehicleEntity")]
    public class VehicleEntity
    {
        [PrimaryKey]
        public string VehicleId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Bearing { get; set; }
        public DateTime LastUpdated { get; set; }
        
        // Foreign key
        [Indexed]
        public string RouteId { get; set; }
    }
}
