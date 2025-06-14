using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Graphics;
using TransportTracker.App.Core.Charts;

namespace TransportTracker.App.Views.Charts
{
    /// <summary>
    /// Line chart view for displaying route frequency over time.
    /// </summary>
    public class FrequencyLineChartView : BaseChartView
    {
        /// <summary>
        /// Bindable property for line color.
        /// </summary>
        public static readonly BindableProperty LineColorProperty = BindableProperty.Create(
            propertyName: nameof(LineColor),
            returnType: typeof(Color),
            declaringType: typeof(FrequencyLineChartView),
            defaultValue: Colors.Blue,
            propertyChanged: (bindable, oldValue, newValue) => ((FrequencyLineChartView)bindable).InvalidateSurface());

        /// <summary>
        /// Bindable property for point size.
        /// </summary>
        public static readonly BindableProperty PointSizeProperty = BindableProperty.Create(
            propertyName: nameof(PointSize),
            returnType: typeof(float),
            declaringType: typeof(FrequencyLineChartView),
            defaultValue: 8f,
            propertyChanged: (bindable, oldValue, newValue) => ((FrequencyLineChartView)bindable).InvalidateSurface());

        /// <summary>
        /// Bindable property for line thickness.
        /// </summary>
        public static readonly BindableProperty LineThicknessProperty = BindableProperty.Create(
            propertyName: nameof(LineThickness),
            returnType: typeof(float),
            declaringType: typeof(FrequencyLineChartView),
            defaultValue: 3f,
            propertyChanged: (bindable, oldValue, newValue) => ((FrequencyLineChartView)bindable).InvalidateSurface());

        /// <summary>
        /// Bindable property to indicate whether the area under the line should be filled.
        /// </summary>
        public static readonly BindableProperty FillAreaProperty = BindableProperty.Create(
            propertyName: nameof(FillArea),
            returnType: typeof(bool),
            declaringType: typeof(FrequencyLineChartView),
            defaultValue: true,
            propertyChanged: (bindable, oldValue, newValue) => ((FrequencyLineChartView)bindable).InvalidateSurface());

        /// <summary>
        /// Bindable property to indicate whether points should be displayed.
        /// </summary>
        public static readonly BindableProperty ShowPointsProperty = BindableProperty.Create(
            propertyName: nameof(ShowPoints),
            returnType: typeof(bool),
            declaringType: typeof(FrequencyLineChartView),
            defaultValue: true,
            propertyChanged: (bindable, oldValue, newValue) => ((FrequencyLineChartView)bindable).InvalidateSurface());

        /// <summary>
        /// Gets or sets the line color.
        /// </summary>
        public Color LineColor
        {
            get => (Color)GetValue(LineColorProperty);
            set => SetValue(LineColorProperty, value);
        }

        /// <summary>
        /// Gets or sets the point size.
        /// </summary>
        public float PointSize
        {
            get => (float)GetValue(PointSizeProperty);
            set => SetValue(PointSizeProperty, value);
        }

        /// <summary>
        /// Gets or sets the line thickness.
        /// </summary>
        public float LineThickness
        {
            get => (float)GetValue(LineThicknessProperty);
            set => SetValue(LineThicknessProperty, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether the area under the line should be filled.
        /// </summary>
        public bool FillArea
        {
            get => (bool)GetValue(FillAreaProperty);
            set => SetValue(FillAreaProperty, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether points should be displayed.
        /// </summary>
        public bool ShowPoints
        {
            get => (bool)GetValue(ShowPointsProperty);
            set => SetValue(ShowPointsProperty, value);
        }

        /// <summary>
        /// Draws the frequency line chart.
        /// </summary>
        /// <param name="canvas">Canvas to draw on.</param>
        /// <param name="dirtyRect">Size of the drawing area.</param>
        public override void DrawChart(ICanvas canvas, RectF dirtyRect)
        {
            if (Entries == null || !Entries.Any())
                return;

            // Calculate max value
            float maxValue = Entries.Max(e => e.Value);
            maxValue *= 1.1f; // Add a little headroom

            // Draw title
            if (!string.IsNullOrEmpty(Title))
            {
                canvas.FontColor = LabelTextColor;
                canvas.FontSize = 18;
                canvas.DrawString(Title, dirtyRect.Width / 2, 20, HorizontalAlignment.Center);
            }

            // Calculate chart area
            float chartTop = 50;
            float chartBottom = dirtyRect.Height - 60;
            float chartLeft = 60;
            float chartRight = dirtyRect.Width - 20;
            float chartHeight = chartBottom - chartTop;
            float chartWidth = chartRight - chartLeft;

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

            // Draw x-axis grid lines and labels
            int entryCount = Entries.Count();
            if (entryCount > 1)
            {
                // Create points for the line
                var points = new PointF[entryCount];
                int index = 0;
                
                foreach (var entry in Entries)
                {
                    // Calculate point position
                    float x = chartLeft + (index * chartWidth / (entryCount - 1));
                    float y = chartBottom - ((entry.Value / maxValue) * chartHeight);
                    points[index] = new PointF(x, y);

                    // Draw x-axis label if provided
                    if (index % Math.Max(1, entryCount / 5) == 0 && !string.IsNullOrEmpty(entry.Label))
                    {
                        canvas.FontColor = LabelTextColor;
                        canvas.FontSize = 10;
                        canvas.DrawString(entry.Label, x, chartBottom + 10, HorizontalAlignment.Center);
                    }

                    index++;
                }

                // Draw filled area under the line if requested
                if (FillArea)
                {
                    var fillPoints = new List<PointF>(points);
                    fillPoints.Add(new PointF(points.Last().X, chartBottom));
                    fillPoints.Add(new PointF(points.First().X, chartBottom));
                    
                    PathF path = new PathF();
                    path.MoveTo(fillPoints[0]);
                    
                    for (int i = 1; i < fillPoints.Count; i++)
                    {
                        path.LineTo(fillPoints[i]);
                    }

                    canvas.FillColor = LineColor.WithAlpha(0.2f);
                    canvas.FillPath(path);
                }

                // Draw the line
                canvas.StrokeColor = LineColor;
                canvas.StrokeSize = LineThickness;
                canvas.DrawLines(points);

                // Draw points
                if (ShowPoints)
                {
                    foreach (var point in points)
                    {
                        canvas.FillColor = LineColor;
                        canvas.FillCircle(point, PointSize);
                        canvas.StrokeColor = Colors.White;
                        canvas.StrokeSize = 1;
                        canvas.DrawCircle(point, PointSize);
                    }
                }

                // Draw highlighted points
                index = 0;
                foreach (var entry in Entries)
                {
                    if (entry.IsHighlighted)
                    {
                        var point = points[index];
                        
                        // Draw highlight circle
                        canvas.StrokeColor = LineColor;
                        canvas.StrokeSize = 2;
                        canvas.DrawCircle(point, PointSize * 2);
                        
                        // Draw value
                        canvas.FontColor = entry.TextColor;
                        canvas.FontSize = 12;
                        canvas.DrawString($"{entry.Value:0}", point.X, point.Y - 20, HorizontalAlignment.Center);
                    }
                    
                    index++;
                }
            }

            // Draw X axis label
            canvas.FontColor = LabelTextColor;
            canvas.FontSize = 14;
            canvas.DrawString("Time of Day", chartLeft + (chartWidth / 2), chartBottom + 40, HorizontalAlignment.Center);
        }
    }
}
