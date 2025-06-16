using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace TransportTracker.App.Core.Converters
{
    /// <summary>
    /// Converts a boolean value to a color
    /// </summary>
    public class BoolToColorConverter : IValueConverter
    {
        /// <summary>
        /// Converts a boolean value to a color
        /// </summary>
        /// <param name="value">Boolean value to convert</param>
        /// <param name="targetType">The type to convert to</param>
        /// <param name="parameter">Optional color pair in format "TrueColor,FalseColor" (e.g. "#FF0000,#00FF00")</param>
        /// <param name="culture">Culture information</param>
        /// <returns>Color corresponding to the boolean value</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                if (parameter is string colors)
                {
                    string[] colorPair = colors.Split(',');
                    if (colorPair.Length == 2)
                    {
                        string colorStr = boolValue ? colorPair[0] : colorPair[1];
                        if (Color.TryParse(colorStr, out var color))
                        {
                            return color;
                        }
                    }
                }
                
                // Default colors if parameter is not provided or invalid
                return boolValue ? Colors.Green : Colors.Gray;
            }
            
            return Colors.Gray;
        }

        /// <summary>
        /// Converts back from a color to a boolean value
        /// </summary>
        /// <remarks>Not implemented</remarks>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
