using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace TransportTracker.App.Core.Converters
{
    /// <summary>
    /// Converts a percentage value (0-100) to a decimal value (0.0-1.0)
    /// </summary>
    public class PercentToDecimalConverter : IValueConverter
    {
        /// <summary>
        /// Converts a percentage value to a decimal value
        /// </summary>
        /// <param name="value">Percentage value (0-100) to convert</param>
        /// <param name="targetType">The type to convert to</param>
        /// <param name="parameter">Optional parameter (not used)</param>
        /// <param name="culture">Culture information</param>
        /// <returns>Decimal value between 0.0 and 1.0</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int intValue)
            {
                return Math.Clamp(intValue / 100.0, 0.0, 1.0);
            }
            else if (value is double doubleValue)
            {
                return Math.Clamp(doubleValue / 100.0, 0.0, 1.0);
            }
            
            return 0.0;
        }

        /// <summary>
        /// Converts back from a decimal value to a percentage value
        /// </summary>
        /// <param name="value">Decimal value (0.0-1.0) to convert</param>
        /// <param name="targetType">The type to convert to</param>
        /// <param name="parameter">Optional parameter (not used)</param>
        /// <param name="culture">Culture information</param>
        /// <returns>Percentage value between 0 and 100</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double doubleValue)
            {
                return (int)(doubleValue * 100.0);
            }
            
            return 0;
        }
    }
}
