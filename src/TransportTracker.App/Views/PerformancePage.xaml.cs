using System;
using System.Threading.Tasks;
using TransportTracker.App.Core.Diagnostics;
using TransportTracker.App.ViewModels;

namespace TransportTracker.App.Views
{
    public partial class PerformancePage : ContentPage
    {
        private readonly PerformanceViewModel _viewModel;
        
        public PerformancePage()
        {
            InitializeComponent();
            _viewModel = BindingContext as PerformanceViewModel;
        }
        
        protected override void OnAppearing()
        {
            base.OnAppearing();
            
            // Register the main UI thread with the performance monitor
            Task.Run(() => 
            {
                PerformanceMonitor.Instance.RegisterCurrentThread("UI Thread", ThreadCategory.UI);
                
                // Record the page navigation as an operation
                using (PerformanceMonitor.Instance.StartOperation("Navigate_PerformancePage"))
                {
                    // The timing is measured until this block exits
                }
            });
        }
        
        /// <summary>
        /// Handles clicks on the Metrics tab button
        /// </summary>
        private void OnMetricsTabClicked(object sender, EventArgs e)
        {
            // Show metrics tab, hide threads tab
            MetricsTab.IsVisible = true;
            ThreadsTab.IsVisible = false;
            
            // Update button styling
            MetricsTabButton.BackgroundColor = Color.FromArgb("#007AFF");
            MetricsTabButton.TextColor = Colors.White;
            ThreadsTabButton.BackgroundColor = Color.FromArgb("#E0E0E0");
            ThreadsTabButton.TextColor = Colors.Black;
            
            // Refresh data
            _viewModel?.RefreshCommand?.Execute(null);
        }
        
        /// <summary>
        /// Handles clicks on the Threads tab button
        /// </summary>
        private void OnThreadsTabClicked(object sender, EventArgs e)
        {
            // Show threads tab, hide metrics tab
            MetricsTab.IsVisible = false;
            ThreadsTab.IsVisible = true;
            
            // Update button styling
            ThreadsTabButton.BackgroundColor = Color.FromArgb("#007AFF");
            ThreadsTabButton.TextColor = Colors.White;
            MetricsTabButton.BackgroundColor = Color.FromArgb("#E0E0E0");
            MetricsTabButton.TextColor = Colors.Black;
            
            // Refresh data
            _viewModel?.RefreshCommand?.Execute(null);
        }
    }
}
