using System;

namespace TransportTracker.Core.Models
{
    /// <summary>
    /// Enumeration of transport vehicle types
    /// </summary>
    public enum TransportType
    {
        /// <summary>
        /// Unknown transport type
        /// </summary>
        Unknown = 0,
        
        /// <summary>
        /// Bus
        /// </summary>
        Bus = 1,
        
        /// <summary>
        /// Tram
        /// </summary>
        Tram = 2,
        
        /// <summary>
        /// Subway/Metro
        /// </summary>
        Subway = 3,
        
        /// <summary>
        /// Train
        /// </summary>
        Train = 4,
        
        /// <summary>
        /// Ferry/Boat
        /// </summary>
        Ferry = 5,
        
        /// <summary>
        /// Cable Car
        /// </summary>
        CableCar = 6,
        
        /// <summary>
        /// Gondola/Suspended cable car
        /// </summary>
        Gondola = 7,
        
        /// <summary>
        /// Funicular (rail system for steep inclines)
        /// </summary>
        Funicular = 8,
        
        /// <summary>
        /// Taxi or rideshare service
        /// </summary>
        Taxi = 9
    }
}
