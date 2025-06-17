using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using TransportTracker.App.ViewModels;
using TransportTracker.App.Views.Maps;
using TransportTracker.App.Views.Vehicles;

namespace TransportTracker.App.Core.Search
{
    /// <summary>
    /// Search handler for transport vehicles that integrates with the shell search box
    /// </summary>
    public class VehicleSearchHandler : SearchHandler
    {
        protected VehiclesViewModel ViewModel => BindingContext as VehiclesViewModel;
        
        /// <summary>
        /// Gets or sets the last search query that was processed
        /// </summary>
        public string LastQuery { get; private set; }
        
        /// <summary>
        /// Initializes a new instance of the VehicleSearchHandler
        /// </summary>
        public VehicleSearchHandler()
        {
            Placeholder = "Search vehicles...";
            ShowsResults = true;
            ClearPlaceholderCommand = new Command(() => { Query = string.Empty; });
            ClearSearchCommand = new Command(() => 
            { 
                Query = string.Empty; 
                ViewModel?.ClearSearchCommand.Execute(null);
            });
            
            QueryChanged += OnQueryChanged;
        }
        
        private void OnQueryChanged(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(Query) || Query.Length < 2)
            {
                ItemsSource = null;
                return;
            }
            
            // Only trigger search after a small delay to avoid too many searches while typing
            Task.Delay(300).ContinueWith(_ => ProcessSearch());
        }
        
        private void ProcessSearch()
        {
            if (ViewModel == null || string.IsNullOrWhiteSpace(Query) || Query == LastQuery)
                return;
                
            LastQuery = Query;
            
            // Execute the search in the view model and also prepare a preview result list
            MainThread.BeginInvokeOnMainThread(() => 
            {
                ViewModel.SearchCommand.Execute(Query);
                
                // Create a preview of results for the dropdown
                if (ViewModel.FilteredVehicles != null)
                {
                    var previewResults = ViewModel.FilteredVehicles.Take(5).ToList();
                    ItemsSource = previewResults;
                }
            });
        }
        
        protected override void OnItemSelected(object item)
        {
            base.OnItemSelected(item);
            
            if (item is TransportVehicle vehicle)
            {
                // Select the vehicle in the view model
                ViewModel?.VehicleSelectedCommand?.Execute(vehicle);
            }
        }
    }
}
