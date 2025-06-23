using System;
using System.Collections.Generic;

namespace ThreadingCS.Models
{
    public class TransportRoute
    {
        public string RouteId { get; set; }
        public string RouteName { get; set; }
        public string AgencyName { get; set; }
        public List<TransportStop> Stops { get; set; } = new List<TransportStop>();
        public List<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
        public string Color { get; set; } = "#FF0000";
        public double Duration { get; set; }
        public double Distance { get; set; }
    }

    public class TransportStop
    {
        public string StopId { get; set; }
        public string StopName { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public DateTime EstimatedArrival { get; set; }
    }

    public class Vehicle
    {
        public string VehicleId { get; set; }
        public string RouteId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Bearing { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class TransportApiResponse
    {
        public List<TransportRoute> Routes { get; set; } = new List<TransportRoute>();
        public int TotalCount { get; set; }
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }
    }
}
