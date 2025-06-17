using System;
using TransportTracker.App.ViewModels;

namespace TransportTracker.App.Views
{
    public partial class StatisticsPage : ContentPage
    {
        private readonly TransportStatisticsViewModel _viewModel;
        
        public StatisticsPage()
        {
            InitializeComponent();
            _viewModel = BindingContext as TransportStatisticsViewModel;
        }
        
        protected override void OnAppearing()
        {
            base.OnAppearing();
            // Refresh data if needed
            if (_viewModel != null && !_viewModel.HasData)
            {
                _viewModel.RefreshCommand.Execute(null);
            }
        }
    }
}
