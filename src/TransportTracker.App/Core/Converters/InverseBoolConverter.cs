using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace TransportTracker.App.Core.Converters
{
    /// <summary>
    /// Converts a boolean value to its inverse
    /// </summary>
    public class InverseBoolConverter : IValueConverter
    {
        /// <summary>
        /// Converts a boolean value to its inverse
        /// </summary>
        /// <param name="value">Boolean value to convert</param>
        /// <param name="targetType">The type to convert to</param>
        /// <param name="parameter">Optional parameter (not used)</param>
        /// <param name="culture">Culture information</param>
        /// <returns>The inverse of the boolean value</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            
            return true;
        }

        /// <summary>
        /// Converts back from an inverse boolean value to the original boolean value
        /// </summary>
        /// <param name="value">Boolean value to convert</param>
        /// <param name="targetType">The type to convert to</param>
        /// <param name="parameter">Optional parameter (not used)</param>
        /// <param name="culture">Culture information</param>
        /// <returns>The inverse of the boolean value</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Convert(value, targetType, parameter, culture);
        }
    }
}
