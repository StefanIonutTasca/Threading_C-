using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Maps;
using CommunityToolkit.Maui;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Maps;
using TransportTracker.App.Core.MVVM;
using TransportTracker.App.Services;
using TransportTracker.App.ViewModels;
using TransportTracker.App.Views.Charts;
using TransportTracker.App.Views.Maps;
using TransportTracker.App.Views.Vehicles;

namespace TransportTracker.App
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .UseMauiMaps()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                })
                .ConfigureMauiHandlers(handlers =>
                {
                    // Register custom handlers here (will be registered at runtime)
#if ANDROID
                    handlers.AddHandler<Microsoft.Maui.Controls.Maps.Map, Platforms.Android.Renderers.CustomMapPinRenderer>();
#endif
                });

            // Register services
            builder.Services.AddSingleton<INavigationService, NavigationService>();
            // TODO: Implement IMapService and MapService or add the correct using directive if they exist
// builder.Services.AddSingleton<IMapService, MapService>();
            builder.Services.AddSingleton<IVehiclesService, VehiclesService>();
            
            // Register ViewModelLocator
            builder.Services.AddSingleton<ViewModelLocator>();
            
            // Register view models
            builder.Services.AddSingleton<MainViewModel>();
            builder.Services.AddSingleton<MapViewModel>();
            builder.Services.AddSingleton<ChartsViewModel>();
            builder.Services.AddSingleton<VehiclesViewModel>();
            builder.Services.AddTransient<VehicleDetailsViewModel>();
            builder.Services.AddSingleton<SettingsViewModel>();
            
            // Register views
            builder.Services.AddSingleton<MapView>();
            builder.Services.AddSingleton<ChartsView>();
            builder.Services.AddSingleton<VehiclesView>();
            builder.Services.AddTransient<VehicleDetailsPage>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
