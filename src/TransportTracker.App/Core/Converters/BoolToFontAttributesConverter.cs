using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace TransportTracker.App.Core.Converters
{
    /// <summary>
    /// Converts a boolean value to FontAttributes
    /// </summary>
    public class BoolToFontAttributesConverter : IValueConverter
    {
        /// <summary>
        /// Converts a boolean value to FontAttributes
        /// </summary>
        /// <param name="value">Boolean value to convert</param>
        /// <param name="targetType">The type to convert to</param>
        /// <param name="parameter">Optional parameter (not used)</param>
        /// <param name="culture">Culture information</param>
        /// <returns>FontAttributes.Bold if true, FontAttributes.None if false</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? FontAttributes.Bold : FontAttributes.None;
            }
            
            return FontAttributes.None;
        }

        /// <summary>
        /// Converts back from FontAttributes to a boolean value
        /// </summary>
        /// <remarks>Not implemented</remarks>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
