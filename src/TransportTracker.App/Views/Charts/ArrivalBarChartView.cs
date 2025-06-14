using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Graphics;
using TransportTracker.App.Core.Charts;

namespace TransportTracker.App.Views.Charts
{
    /// <summary>
    /// Bar chart view specialized for displaying arrival predictions.
    /// </summary>
    public class ArrivalBarChartView : BaseChartView
    {
        /// <summary>
        /// Bindable property for maximum value.
        /// </summary>
        public static readonly BindableProperty MaxValueProperty = BindableProperty.Create(
            propertyName: nameof(MaxValue),
            returnType: typeof(float),
            declaringType: typeof(ArrivalBarChartView),
            defaultValue: 0f,
            propertyChanged: (bindable, oldValue, newValue) => ((ArrivalBarChartView)bindable).InvalidateSurface());

        /// <summary>
        /// Bindable property for bar spacing.
        /// </summary>
        public static readonly BindableProperty BarSpacingProperty = BindableProperty.Create(
            propertyName: nameof(BarSpacing),
            returnType: typeof(float),
            declaringType: typeof(ArrivalBarChartView),
            defaultValue: 10f,
            propertyChanged: (bindable, oldValue, newValue) => ((ArrivalBarChartView)bindable).InvalidateSurface());

        /// <summary>
        /// Bindable property for current time indicator.
        /// </summary>
        public static readonly BindableProperty ShowCurrentTimeProperty = BindableProperty.Create(
            propertyName: nameof(ShowCurrentTime),
            returnType: typeof(bool),
            declaringType: typeof(ArrivalBarChartView),
            defaultValue: true,
            propertyChanged: (bindable, oldValue, newValue) => ((ArrivalBarChartView)bindable).InvalidateSurface());

        /// <summary>
        /// Bindable property for current time indicator color.
        /// </summary>
        public static readonly BindableProperty CurrentTimeColorProperty = BindableProperty.Create(
            propertyName: nameof(CurrentTimeColor),
            returnType: typeof(Color),
            declaringType: typeof(ArrivalBarChartView),
            defaultValue: Colors.Red,
            propertyChanged: (bindable, oldValue, newValue) => ((ArrivalBarChartView)bindable).InvalidateSurface());

        /// <summary>
        /// Gets or sets the maximum value for the chart.
        /// </summary>
        public float MaxValue
        {
            get => (float)GetValue(MaxValueProperty);
            set => SetValue(MaxValueProperty, value);
        }

        /// <summary>
        /// Gets or sets the spacing between bars.
        /// </summary>
        public float BarSpacing
        {
            get => (float)GetValue(BarSpacingProperty);
            set => SetValue(BarSpacingProperty, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether to show current time indicator.
        /// </summary>
        public bool ShowCurrentTime
        {
            get => (bool)GetValue(ShowCurrentTimeProperty);
            set => SetValue(ShowCurrentTimeProperty, value);
        }

        /// <summary>
        /// Gets or sets the color of the current time indicator.
        /// </summary>
        public Color CurrentTimeColor
        {
            get => (Color)GetValue(CurrentTimeColorProperty);
            set => SetValue(CurrentTimeColorProperty, value);
        }

        /// <summary>
        /// Draws the arrival bar chart.
        /// </summary>
        /// <param name="canvas">Canvas to draw on.</param>
        /// <param name="dirtyRect">Size of the drawing area.</param>
        public override void DrawChart(ICanvas canvas, RectF dirtyRect)
        {
            if (Entries == null || !Entries.Any())
                return;

            // Calculate max value if not explicitly set
            float maxValue = MaxValue;
            if (maxValue <= 0)
            {
                maxValue = Entries.Max(e => e.Value);
                // Add a little headroom
                maxValue *= 1.1f;
            }

            // Draw title
            if (!string.IsNullOrEmpty(Title))
            {
                canvas.FontColor = LabelTextColor;
                canvas.FontSize = 18;
                canvas.DrawString(Title, dirtyRect.Width / 2, 20, HorizontalAlignment.Center);
            }

            // Calculate chart area (leaving space for title, labels and legend)
            float chartTop = 50;
            float chartBottom = dirtyRect.Height - 60; // Space for x-axis labels
            float chartLeft = 60; // Space for y-axis
            float chartRight = dirtyRect.Width - 20;
            float chartHeight = chartBottom - chartTop;
            float chartWidth = chartRight - chartLeft;

            // Draw chart background
            var chartBackgroundRect = new RectF(chartLeft, chartTop, chartWidth, chartHeight);
            canvas.StrokeColor = Colors.LightGray;
            canvas.DrawRectangle(chartBackgroundRect);

            // Draw y-axis grid lines and labels
            int yDivisions = 5;
            float yStep = maxValue / yDivisions;
            for (int i = 0; i <= yDivisions; i++)
            {
                float y = chartBottom - (i * chartHeight / yDivisions);
                canvas.StrokeColor = Colors.LightGray;
                canvas.DrawLine(chartLeft, y, chartRight, y);

                float yValue = i * yStep;
                canvas.FontSize = 12;
                canvas.FontColor = LabelTextColor;
                canvas.DrawString($"{yValue:0}", chartLeft - 10, y, HorizontalAlignment.Right);
            }

            // Calculate bar width
            int entryCount = Entries.Count();
            float barWidth = (chartWidth - ((entryCount - 1) * BarSpacing)) / entryCount;

            // Draw bars
            int index = 0;
            foreach (var entry in Entries)
            {
                // Calculate bar position
                float barX = chartLeft + (index * (barWidth + BarSpacing));
                float barHeight = (entry.Value / maxValue) * chartHeight;
                float barY = chartBottom - barHeight;

                // Draw bar
                canvas.FillColor = entry.IsHighlighted ? entry.Color.WithAlpha(1.0f) : entry.Color.WithAlpha(0.7f);
                canvas.FillRectangle(barX, barY, barWidth, barHeight);

                // Draw label
                if (!string.IsNullOrEmpty(entry.Label))
                {
                    canvas.FontColor = LabelTextColor;
                    canvas.FontSize = 10;
                    canvas.DrawString(entry.Label, barX + (barWidth / 2), chartBottom + 10, HorizontalAlignment.Center);
                }

                // Draw value on top of bar
                canvas.FontColor = entry.TextColor;
                canvas.FontSize = 10;
                canvas.DrawString($"{entry.Value:0}", barX + (barWidth / 2), barY - 10, HorizontalAlignment.Center);

                index++;
            }

            // Draw "current time" indicator if enabled
            if (ShowCurrentTime)
            {
                // Calculate position (assuming entries are time-based and "now" is at the left edge)
                float timeX = chartLeft + (chartWidth * 0.2f); // 20% into the chart
                
                // Draw vertical line
                canvas.StrokeColor = CurrentTimeColor;
                canvas.StrokeSize = 2;
                canvas.DrawLine(timeX, chartTop, timeX, chartBottom);
                
                // Draw "Now" label
                canvas.FontColor = CurrentTimeColor;
                canvas.FontSize = 12;
                canvas.DrawString("Now", timeX, chartTop - 10, HorizontalAlignment.Center);
            }

            // Draw X axis label
            canvas.FontColor = LabelTextColor;
            canvas.FontSize = 14;
            canvas.DrawString("Arrival Time", chartLeft + (chartWidth / 2), chartBottom + 40, HorizontalAlignment.Center);
        }
    }
}
