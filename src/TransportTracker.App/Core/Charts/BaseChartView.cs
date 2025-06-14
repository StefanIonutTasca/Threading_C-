using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace TransportTracker.App.Core.Charts
{
    /// <summary>
    /// Abstract base class for all chart views in the application.
    /// </summary>
    public abstract class BaseChartView : GraphicsView
    {
        /// <summary>
        /// Bindable property for chart entries.
        /// </summary>
        public static readonly BindableProperty EntriesProperty = BindableProperty.Create(
            propertyName: nameof(Entries),
            returnType: typeof(IEnumerable<ChartEntry>),
            declaringType: typeof(BaseChartView),
            defaultValue: null,
            propertyChanged: OnEntriesPropertyChanged);

        /// <summary>
        /// Bindable property for chart title.
        /// </summary>
        public static readonly BindableProperty TitleProperty = BindableProperty.Create(
            propertyName: nameof(Title),
            returnType: typeof(string),
            declaringType: typeof(BaseChartView),
            defaultValue: string.Empty,
            propertyChanged: (bindable, oldValue, newValue) => ((BaseChartView)bindable).InvalidateSurface());

        /// <summary>
        /// Bindable property for chart background color.
        /// </summary>
        public static readonly BindableProperty BackgroundColorProperty = BindableProperty.Create(
            propertyName: nameof(BackgroundColor),
            returnType: typeof(Color),
            declaringType: typeof(BaseChartView),
            defaultValue: Colors.White,
            propertyChanged: (bindable, oldValue, newValue) => ((BaseChartView)bindable).InvalidateSurface());

        /// <summary>
        /// Bindable property for label text color.
        /// </summary>
        public static readonly BindableProperty LabelTextColorProperty = BindableProperty.Create(
            propertyName: nameof(LabelTextColor),
            returnType: typeof(Color),
            declaringType: typeof(BaseChartView),
            defaultValue: Colors.Gray,
            propertyChanged: (bindable, oldValue, newValue) => ((BaseChartView)bindable).InvalidateSurface());

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseChartView"/> class.
        /// </summary>
        protected BaseChartView()
        {
            Drawable = new ChartDrawable(this);
            BackgroundColor = Colors.Transparent;
        }

        /// <summary>
        /// Gets or sets the entries for the chart.
        /// </summary>
        public IEnumerable<ChartEntry> Entries
        {
            get => (IEnumerable<ChartEntry>)GetValue(EntriesProperty);
            set => SetValue(EntriesProperty, value);
        }

        /// <summary>
        /// Gets or sets the title of the chart.
        /// </summary>
        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        /// <summary>
        /// Gets or sets the background color of the chart.
        /// </summary>
        public new Color BackgroundColor
        {
            get => (Color)GetValue(BackgroundColorProperty);
            set => SetValue(BackgroundColorProperty, value);
        }

        /// <summary>
        /// Gets or sets the text color for labels.
        /// </summary>
        public Color LabelTextColor
        {
            get => (Color)GetValue(LabelTextColorProperty);
            set => SetValue(LabelTextColorProperty, value);
        }

        /// <summary>
        /// Draw the chart with the given canvas, dimensions.
        /// </summary>
        /// <param name="canvas">Canvas to draw on.</param>
        /// <param name="dirtyRect">Size of the drawing area.</param>
        public abstract void DrawChart(ICanvas canvas, RectF dirtyRect);

        private static void OnEntriesPropertyChanged(BindableObject bindable, object oldValue, object newValue)
        {
            var chartView = (BaseChartView)bindable;
            
            // If old value was an observable collection, unsubscribe from change events
            if (oldValue is INotifyCollectionChanged oldCollection)
            {
                oldCollection.CollectionChanged -= chartView.OnEntriesCollectionChanged;
            }

            // If new value is an observable collection, subscribe to change events
            if (newValue is INotifyCollectionChanged newCollection)
            {
                newCollection.CollectionChanged += chartView.OnEntriesCollectionChanged;
            }

            chartView.InvalidateSurface();
        }

        private void OnEntriesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            InvalidateSurface();
        }

        /// <summary>
        /// Drawable implementation for the chart.
        /// </summary>
        private class ChartDrawable : IDrawable
        {
            private readonly BaseChartView _chart;

            public ChartDrawable(BaseChartView chart)
            {
                _chart = chart;
            }

            public void Draw(ICanvas canvas, RectF dirtyRect)
            {
                // Set canvas to the chart background color
                canvas.FillColor = _chart.BackgroundColor;
                canvas.FillRectangle(dirtyRect);

                // Draw the chart if we have entries
                if (_chart.Entries != null && _chart.Entries.Any())
                {
                    _chart.DrawChart(canvas, dirtyRect);
                }
                else
                {
                    // Draw a placeholder or "No data" message
                    canvas.FontColor = _chart.LabelTextColor;
                    canvas.FontSize = 14;
                    var text = "No data available";
                    canvas.DrawString(text, dirtyRect.Center.X - 50, dirtyRect.Center.Y, HorizontalAlignment.Left);
                }
            }
        }
    }
}
