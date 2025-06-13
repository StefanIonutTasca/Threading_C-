using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Maps;
using TransportTracker.App.Core.MVVM;
using TransportTracker.App.Services;
using TransportTracker.App.ViewModels;
using TransportTracker.App.Views.Maps;

namespace TransportTracker.App
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
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
            
            // Register ViewModelLocator
            builder.Services.AddSingleton<ViewModelLocator>();
            
            // Register view models
            builder.Services.AddTransient<MainViewModel>();
            builder.Services.AddTransient<MapViewModel>();
            builder.Services.AddTransient<VehiclesViewModel>();
            builder.Services.AddSingleton<SettingsViewModel>();
            
            // Register views
            builder.Services.AddTransient<MapView>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
