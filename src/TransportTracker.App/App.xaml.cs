using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using TransportTracker.App.Services;

namespace TransportTracker.App
{
    public partial class App : Application
    {
        public static IServiceProvider ServiceProvider { get; private set; }

        public App()
        {
            InitializeComponent();

            // Setup dependency injection
            SetupServices();

            MainPage = new AppShell();
        }

        private void SetupServices()
        {
            var services = new ServiceCollection();
            
            // Register services for dependency injection
            services.AddSingleton<INavigationService, NavigationService>();
            
            // Create service provider
            ServiceProvider = services.BuildServiceProvider();
        }

        public static TService GetService<TService>()
            => ServiceProvider.GetService<TService>();

        protected override void OnStart()
        {
            // Handle when your app starts
        }

        protected override void OnSleep()
        {
            // Handle when your app sleeps
        }

        protected override void OnResume()
        {
            // Handle when your app resumes
        }
    }
}
