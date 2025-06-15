using System.Globalization;
using Microsoft.Maui.Graphics;

namespace TransportTracker.App.Core.Converters
{
    /// <summary>
    /// Converts transport vehicle status to appropriate color representation.
    /// </summary>
    public class StatusToColorConverter : IValueConverter
    {
        /// <summary>
        /// Converts a vehicle status to a color.
        /// </summary>
        /// <param name="value">The vehicle status as a string.</param>
        /// <param name="targetType">The type of the binding target property.</param>
        /// <param name="parameter">Additional parameter for the converter.</param>
        /// <param name="culture">The culture to use in the converter.</param>
        /// <returns>A Color object representing the status.</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string status)
            {
                return status.ToLowerInvariant() switch
                {
                    "on time" => Color.FromArgb("#107C10"),      // Green
                    "early" => Color.FromArgb("#2D7D9A"),        // Light Blue
                    "slight delay" => Color.FromArgb("#F7630C"), // Light Orange
                    "delayed" => Color.FromArgb("#D83B01"),      // Orange
                    "significant delay" => Color.FromArgb("#C50F1F"), // Red
                    "cancelled" => Color.FromArgb("#5A5A5A"),    // Dark Gray
                    _ => Color.FromArgb("#605E5C")               // Gray
                };
            }

            return Color.FromArgb("#605E5C"); // Default gray
        }

        /// <summary>
        /// Converts a color back to a status (not implemented).
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
