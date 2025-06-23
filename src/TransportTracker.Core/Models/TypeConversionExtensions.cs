using System;

namespace TransportTracker.Core.Models
{
    /// <summary>
    /// Extension methods for type conversions between related enums
    /// </summary>
    public static class TypeConversionExtensions
    {
        /// <summary>
        /// Converts a TransportType to a VehicleType
        /// </summary>
        /// <param name="transportType">The TransportType to convert</param>
        /// <returns>Corresponding VehicleType</returns>
        public static VehicleType ToVehicleType(this TransportType transportType)
        {
            return transportType switch
            {
                TransportType.Bus => VehicleType.Bus,
                TransportType.Train => VehicleType.Train,
                TransportType.Tram => VehicleType.Tram,
                TransportType.Subway => VehicleType.Metro,
                TransportType.Ferry => VehicleType.Ferry,
                TransportType.Taxi => VehicleType.Taxi,
                _ => VehicleType.Other
            };
        }

        /// <summary>
        /// Converts a VehicleType to a TransportType
        /// </summary>
        /// <param name="vehicleType">The VehicleType to convert</param>
        /// <returns>Corresponding TransportType</returns>
        public static TransportType ToTransportType(this VehicleType vehicleType)
        {
            return vehicleType switch
            {
                VehicleType.Bus => TransportType.Bus,
                VehicleType.Train => TransportType.Train,
                VehicleType.Tram => TransportType.Tram,
                VehicleType.Metro => TransportType.Subway,
                VehicleType.Ferry => TransportType.Ferry,
                VehicleType.Taxi => TransportType.Taxi,
                _ => TransportType.Unknown
            };
        }
    }
}
