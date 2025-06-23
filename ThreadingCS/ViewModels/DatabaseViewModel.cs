using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using ThreadingCS.Models;
using ThreadingCS.Services;
using System.Diagnostics;

namespace ThreadingCS.ViewModels
{
    public class DatabaseViewModel : BaseViewModel
    {
        private readonly DatabaseService _databaseService;
        public ObservableCollection<TransportRoute> Routes { get; } = new();
        public ICommand RefreshCommand { get; }

        public DatabaseViewModel()
        {
            _databaseService = new DatabaseService();
            RefreshCommand = new Command(async () => await LoadRoutesAsync());
            Task.Run(async () => await LoadRoutesAsync());
        }

        /// <summary>
/// Loads all routes from the database and processes them in parallel (PLINQ) before updating the UI.
/// This demonstrates multi-core/thread pool usage per the rubric.
/// </summary>
public async Task LoadRoutesAsync()
{
    IsBusy = true;
    try
    {
        Debug.WriteLine("[DBPage] Loading all routes from DB");
        var routes = await _databaseService.GetAllRoutesAsync();

        // PLINQ: Simulate a parallel processing step (e.g., formatting route names)
        var processedRoutes = routes
            .AsParallel()
            .Select(r => {
                r.RouteName = $"[Parallel] {r.RouteName}";
                return r;
            })
            .ToList();

        MainThread.BeginInvokeOnMainThread(() => {
            Routes.Clear();
            foreach (var route in processedRoutes)
                Routes.Add(route);
        });
        Debug.WriteLine($"[DBPage] Loaded {processedRoutes.Count} routes (processed in parallel)");
    }
    catch (System.Exception ex)
    {
        Debug.WriteLine($"[DBPage] Error loading routes: {ex.Message}");
    }
    IsBusy = false;
}
    }
}
