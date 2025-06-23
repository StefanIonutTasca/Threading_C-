using ThreadingCS.ViewModels;

namespace ThreadingCS.Views
{
    public partial class GraphsPage : ContentPage
    {
        private readonly GraphsViewModel _viewModel;
        private bool _isUpdating = false;

        public GraphsPage()
        {
            InitializeComponent();
            _viewModel = new GraphsViewModel();
            BindingContext = _viewModel;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _viewModel.SetBarChartContainer(BarChartContainer);
        }

        private async void OnGenerateDataClicked(object sender, EventArgs e)
        {
            await _viewModel.GenerateChartDataAsync();
        }

        private async void OnLiveUpdatesClicked(object sender, EventArgs e)
        {
            if (_isUpdating)
            {
                _viewModel.StopLiveUpdates();
                LiveUpdatesButton.Text = "Start Live Updates";
                LiveUpdatesButton.BackgroundColor = Color.FromArgb("#2196F3");
                _isUpdating = false;
            }
            else
            {
                await _viewModel.StartLiveUpdatesAsync();
                LiveUpdatesButton.Text = "Stop Live Updates";
                LiveUpdatesButton.BackgroundColor = Color.FromArgb("#F44336");
                _isUpdating = true;
            }
        }
    }
}
