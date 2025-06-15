using System.Globalization;
using Microsoft.Maui.Graphics;

namespace TransportTracker.App.Core.Converters
{
    /// <summary>
    /// Converts transport vehicle types to appropriate color representation.
    /// </summary>
    public class TypeToColorConverter : IValueConverter
    {
        /// <summary>
        /// Converts a vehicle type to a color.
        /// </summary>
        /// <param name="value">The vehicle type as a string.</param>
        /// <param name="targetType">The type of the binding target property.</param>
        /// <param name="parameter">Additional parameter for the converter.</param>
        /// <param name="culture">The culture to use in the converter.</param>
        /// <returns>A Color object representing the vehicle type.</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string vehicleType)
            {
                return vehicleType.ToLowerInvariant() switch
                {
                    "bus" => Color.FromArgb("#0078D4"),    // Blue
                    "train" => Color.FromArgb("#107C10"),  // Green
                    "tram" => Color.FromArgb("#D83B01"),   // Orange
                    "subway" => Color.FromArgb("#5C2D91"), // Purple
                    "ferry" => Color.FromArgb("#008575"),  // Teal
                    _ => Color.FromArgb("#605E5C")         // Gray
                };
            }

            return Color.FromArgb("#605E5C"); // Default gray
        }

        /// <summary>
        /// Converts a color back to a vehicle type (not implemented).
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <param name="targetType">The type to convert to.</param>
        /// <param name="parameter">Additional parameter for the converter.</param>
        /// <param name="culture">The culture to use in the converter.</param>
        /// <returns>The converted value.</returns>
        /// <exception cref="NotImplementedException">This method is not implemented.</exception>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
