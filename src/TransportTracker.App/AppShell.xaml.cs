using Microsoft.Maui.Controls;
using System;
using TransportTracker.App.Views;

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
            
            // These routes will be implemented as pages are created
            Routing.RegisterRoute("MapPage", typeof(MainPage));
            Routing.RegisterRoute("VehiclesPage", typeof(MainPage));
            Routing.RegisterRoute("RoutesPage", typeof(MainPage));
            Routing.RegisterRoute("AnalyticsPage", typeof(MainPage));
            Routing.RegisterRoute("SearchPage", typeof(MainPage));
            Routing.RegisterRoute("FavoritesPage", typeof(MainPage));
            Routing.RegisterRoute("SettingsPage", typeof(MainPage));
            Routing.RegisterRoute("AboutPage", typeof(MainPage));
            
            // Route for vehicle details (will be implemented later)
            Routing.RegisterRoute("VehicleDetailsPage", typeof(MainPage));
        }
    }
}
