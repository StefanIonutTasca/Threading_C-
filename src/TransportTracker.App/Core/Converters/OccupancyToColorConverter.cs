using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace TransportTracker.App.Core.Converters
{
    /// <summary>
    /// Converts an occupancy percentage value to a color
    /// </summary>
    public class OccupancyToColorConverter : IValueConverter
    {
        /// <summary>
        /// Converts an occupancy percentage value to a color
        /// </summary>
        /// <param name="value">Occupancy percentage (0-100) to convert</param>
        /// <param name="targetType">The type to convert to</param>
        /// <param name="parameter">Optional parameter (not used)</param>
        /// <param name="culture">Culture information</param>
        /// <returns>Color representing the occupancy level (green for low, yellow for medium, red for high)</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int intValue)
            {
                return GetOccupancyColor(intValue);
            }
            else if (value is double doubleValue)
            {
                return GetOccupancyColor((int)doubleValue);
            }
            
            return Colors.Green;
        }

        /// <summary>
        /// Converts back from a color to an occupancy percentage value
        /// </summary>
        /// <remarks>Not implemented</remarks>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
        
        private Color GetOccupancyColor(int occupancyPercentage)
        {
            if (occupancyPercentage < 50)
            {
                return Colors.Green;
            }
            else if (occupancyPercentage < 80)
            {
                return Colors.Orange;
            }
            else
            {
                return Colors.Red;
            }
        }
    }
}
