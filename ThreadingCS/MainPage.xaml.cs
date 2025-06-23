using ThreadingCS.ViewModels;

namespace ThreadingCS
{
    public partial class MainPage : ContentPage
    {
        private readonly MainViewModel _viewModel;
        private bool _isMonitoring = false;

        public MainPage()
        {
            InitializeComponent();
            _viewModel = new MainViewModel();
            BindingContext = _viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            // We don't auto-load data to avoid unnecessary API calls
            // User will need to click the Load Data button
        }

        private async void OnLoadDataClicked(object sender, EventArgs e)
        {
            await _viewModel.InitializeAsync();
        }

        private async void OnMonitoringClicked(object sender, EventArgs e)
        {
            if (_isMonitoring)
            {
                _viewModel.StopMonitoring();
                MonitorButton.Text = "Start Monitoring";
                MonitorButton.BackgroundColor = Color.FromArgb("#2196F3");
                _isMonitoring = false;
            }
            else
            {
                await _viewModel.StartMonitoringAsync();
                MonitorButton.Text = "Stop Monitoring";
                MonitorButton.BackgroundColor = Color.FromArgb("#F44336");
                _isMonitoring = true;
            }
        }

        private async void OnProcessLargeDatasetClicked(object sender, EventArgs e)
        {
            await _viewModel.ProcessLargeDatasetAsync();
        }
    }
}
