using ThreadingCS.Views;

namespace ThreadingCS
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            
            // Register routes for navigation
            Routing.RegisterRoute(nameof(MapPage), typeof(MapPage));
            Routing.RegisterRoute(nameof(GraphsPage), typeof(GraphsPage));
        }
    }
}
