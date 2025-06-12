using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using TransportTracker.App.Services;

namespace TransportTracker.App
{
    public partial class App : Application
    {
        // Services are now configured in MauiProgram.cs
        public App()
        {
            InitializeComponent();

            // Create the main shell with navigation
            MainPage = new AppShell();
        }

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
