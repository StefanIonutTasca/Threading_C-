using System;
using System.Globalization;

namespace TransportTracker.App.Core.Converters
{
    /// <summary>
    /// Converter to check if a numeric value is greater than zero
    /// </summary>
    public class IsGreaterThanZeroConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int intValue)
            {
                return intValue > 0;
            }
            
            if (value is double doubleValue)
            {
                return doubleValue > 0;
            }
            
            if (value is decimal decimalValue)
            {
                return decimalValue > 0;
            }
            
            if (value is long longValue)
            {
                return longValue > 0;
            }
            
            return false;
        }
        
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
