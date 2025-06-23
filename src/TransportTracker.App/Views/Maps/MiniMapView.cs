using Microsoft.Maui;
using Microsoft.Maui.Controls;
using MauiMap = Microsoft.Maui.Controls.Maps.Map;
using TransportTracker.App.Views.Maps;
using Microsoft.Maui.Maps;
using System;
using System.Collections.Generic;
using System.Windows.Input;

namespace TransportTracker.App.Views.Maps
{
    /// <summary>
    /// A compact map view for displaying a single vehicle
    /// </summary>
    public class MiniMapView : MauiMap
    {
        /// <summary>
        /// Bindable property for the Vehicle
        /// </summary>
        public static readonly BindableProperty VehicleProperty = BindableProperty.Create(
            nameof(Vehicle),
            typeof(TransportTracker.App.Views.Maps.TransportVehicle),
            typeof(MiniMapView),
            null,
            propertyChanged: OnVehicleChanged);
        
        /// <summary>
        /// Bindable property for IsLoading
        /// </summary>
        public static readonly BindableProperty IsLoadingProperty = BindableProperty.Create(
            nameof(IsLoading),
            typeof(bool),
            typeof(MiniMapView),
            false);
        
        /// <summary>
        /// Bindable property for IsInteractive
        /// </summary>
        public static readonly BindableProperty IsInteractiveProperty = BindableProperty.Create(
            nameof(IsInteractive),
            typeof(bool),
            typeof(MiniMapView),
            true,
            propertyChanged: OnIsInteractiveChanged);
        
        /// <summary>
        /// Gets or sets the vehicle to display on the map
        /// </summary>
        public TransportTracker.App.Views.Maps.TransportVehicle Vehicle
        {
            get => (TransportTracker.App.Views.Maps.TransportVehicle)GetValue(VehicleProperty);
            set => SetValue(VehicleProperty, value);
        }
        
        /// <summary>
        /// Gets or sets a value indicating whether the map is loading
        /// </summary>
        public bool IsLoading
        {
            get => (bool)GetValue(IsLoadingProperty);
            set => SetValue(IsLoadingProperty, value);
        }
        
        /// <summary>
        /// Gets or sets a value indicating whether the map is interactive
        /// </summary>
        public bool IsInteractive
        {
            get => (bool)GetValue(IsInteractiveProperty);
            set => SetValue(IsInteractiveProperty, value);
        }
        
        /// <summary>
        /// Gets or sets the command executed when the map is tapped
        /// </summary>
        public ICommand MapTappedCommand { get; set; }
        
        /// <summary>
        /// Initializes a new instance of the MiniMapView class
        /// </summary>
        public MiniMapView() : base()
        {
            MapType = MapType.Street;
            IsShowingUser = false;
            IsScrollEnabled = true;
            IsZoomEnabled = true;
            HasZoomEnabled = true;
            HasScrollEnabled = true;
            
            // Set default viewport
            MoveToRegion(MapSpan.FromCenterAndRadius(
                new Location(51.5074, -0.1278), // Default to London
                Distance.FromKilometers(1)));
        }
        
        /// <summary>
        /// Called when the Vehicle property changes
        /// </summary>
        private static void OnVehicleChanged(BindableObject bindable, object oldValue, object newValue)
        {
            var mapView = (MiniMapView)bindable;
            mapView.UpdateMauiMap();
        }
        
        /// <summary>
        /// Called when the IsInteractive property changes
        /// </summary>
        private static void OnIsInteractiveChanged(BindableObject bindable, object oldValue, object newValue)
        {
            var mapView = (MiniMapView)bindable;
            var isInteractive = (bool)newValue;
            
            mapView.IsScrollEnabled = isInteractive;
            mapView.IsZoomEnabled = isInteractive;
            mapView.HasScrollEnabled = isInteractive;
            mapView.HasZoomEnabled = isInteractive;
        }
        
        /// <summary>
        /// Updates the map with the current vehicle
        /// </summary>
        private void UpdateMauiMap()
        {
            // Clear existing pins
            Pins.Clear();
            
            var vehicle = Vehicle;
            if (vehicle == null)
                return;
                
            if (vehicle.Latitude == 0 && vehicle.Longitude == 0)
            {
                // If coordinates are not set, use some default values for demonstration
                vehicle.Latitude = 51.5074; // London coordinates as example
                vehicle.Longitude = -0.1278;
            }
            
            // Create pin for vehicle
            var pin = new Pin
            {
                Label = $"{vehicle.Type} {vehicle.Number}",
                Address = vehicle.Route,
                Type = PinType.Place,
                Location = new Location(vehicle.Latitude, vehicle.Longitude)
            };
            
            Pins.Add(pin);
            
            // Move map to vehicle position
            MoveToRegion(MapSpan.FromCenterAndRadius(
                new Location(vehicle.Latitude, vehicle.Longitude),
                Distance.FromKilometers(0.5)));
        }
    }
}
