using Microsoft.Maui.Controls;
using System;
using TransportTracker.App.Views;
using TransportTracker.App.Views.Maps;
using TransportTracker.App.Views.Vehicles;

namespace TransportTracker.App
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            RegisterRoutes();
        }
        
        private void RegisterRoutes()
        {
            // Register routes for navigation
            Routing.RegisterRoute(nameof(MainPage), typeof(MainPage));
            Routing.RegisterRoute(nameof(MapView), typeof(MapView));
            Routing.RegisterRoute(nameof(VehicleDetailsPage), typeof(VehicleDetailsPage));
            
            // These routes will be implemented as pages are created
            Routing.RegisterRoute("RoutesPage", typeof(MainPage));
            Routing.RegisterRoute("AnalyticsPage", typeof(MainPage));
            Routing.RegisterRoute("SearchPage", typeof(MainPage));
            Routing.RegisterRoute("FavoritesPage", typeof(MainPage));
            Routing.RegisterRoute("SettingsPage", typeof(MainPage));
            Routing.RegisterRoute("AboutPage", typeof(MainPage));
        }
    }
}
