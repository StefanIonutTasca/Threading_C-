using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace TransportTracker.App.Core.UI.Converters
{
    /// <summary>
    /// Converts a percentage value (0-100) to a progress value (0-1)
    /// </summary>
    public class PercentageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double doubleValue)
            {
                return doubleValue / 100.0;
            }
            
            return 0;
        }
        
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double doubleValue)
            {
                return doubleValue * 100.0;
            }
            
            return 0;
        }
    }
    
    /// <summary>
    /// Converts a delay percentage to an appropriate color
    /// </summary>
    public class DelayColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double percentage)
            {
                if (percentage <= 5)
                    return Colors.Green;
                if (percentage <= 15)
                    return Colors.Orange;
                
                return Colors.Red;
            }
            
            return Colors.Gray;
        }
        
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return 0;
        }
    }
    
    /// <summary>
    /// Converts a chart value to a height for visualization
    /// </summary>
    public class ChartValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double doubleValue)
            {
                // Scale the value to a reasonable height (between 5 and 120)
                double maxHeight = parameter != null ? System.Convert.ToDouble(parameter) : 120;
                double minHeight = 5;
                
                // Value of 0 should have minimal height
                if (doubleValue <= 0)
                    return minHeight;
                
                // Scale logarithmically to accommodate large ranges
                double scaledValue = Math.Log10(doubleValue + 1) * 30;
                
                // Cap at max height
                return Math.Min(Math.Max(scaledValue, minHeight), maxHeight);
            }
            
            return 5; // Minimum height
        }
        
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return 0;
        }
    }
    
    /// <summary>
    /// Inverts a boolean value
    /// </summary>
    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            
            return false;
        }
        
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            
            return false;
        }
    }
}
