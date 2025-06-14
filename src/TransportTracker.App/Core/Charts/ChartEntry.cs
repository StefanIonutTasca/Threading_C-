using System;
using Microsoft.Maui.Graphics;

namespace TransportTracker.App.Core.Charts
{
    /// <summary>
    /// Represents a single data entry for charts.
    /// </summary>
    public class ChartEntry
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ChartEntry"/> class.
        /// </summary>
        /// <param name="value">The value of the entry.</param>
        public ChartEntry(float value)
        {
            Value = value;
        }

        /// <summary>
        /// Gets or sets the value of the entry.
        /// </summary>
        public float Value { get; set; }

        /// <summary>
        /// Gets or sets the label for the entry.
        /// </summary>
        public string Label { get; set; }

        /// <summary>
        /// Gets or sets the secondary value (optional) of the entry.
        /// </summary>
        public float? SecondaryValue { get; set; }

        /// <summary>
        /// Gets or sets the color of the entry.
        /// </summary>
        public Color Color { get; set; } = Colors.Blue;

        /// <summary>
        /// Gets or sets a value indicating whether this entry is highlighted.
        /// </summary>
        public bool IsHighlighted { get; set; }

        /// <summary>
        /// Gets or sets the text color for this entry.
        /// </summary>
        public Color TextColor { get; set; } = Colors.Black;
    }
}
