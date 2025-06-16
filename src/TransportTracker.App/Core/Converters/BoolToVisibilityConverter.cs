using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace TransportTracker.App.Core.Converters
{
    /// <summary>
    /// Converts a boolean value to a Visibility value
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// Converts a boolean value to a Visibility value
        /// </summary>
        /// <param name="value">Boolean value to convert</param>
        /// <param name="targetType">The type to convert to</param>
        /// <param name="parameter">Optional parameter, if "inverse" will return !value</param>
        /// <param name="culture">Culture information</param>
        /// <returns>true = Visible, false = Collapsed</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                bool inverse = parameter != null && parameter.ToString().ToLower() == "inverse";
                return inverse ? !boolValue : boolValue;
            }
            
            return true;
        }

        /// <summary>
        /// Converts back from a Visibility value to a boolean value
        /// </summary>
        /// <remarks>Not implemented</remarks>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
