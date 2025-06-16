using System;
using System.Diagnostics;
using TransportTracker.App.Core.UI;
using TransportTracker.App.ViewModels;

namespace TransportTracker.App.Views.Vehicles
{
    public partial class VehicleDetailsPage : BaseContentPage
    {
        private bool _isFirstAppearance = true;
        private VehicleDetailsViewModel ViewModel => BindingContext as VehicleDetailsViewModel;
        
        public VehicleDetailsPage()
        {
            InitializeComponent();
        }
        
        protected override void OnAppearing()
        {
            base.OnAppearing();
            
            try
            {
                if (_isFirstAppearance && ViewModel != null)
                {
                    // Load data when the page first appears
                    ViewModel.InitializeCommand.Execute(null);
                    _isFirstAppearance = false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in VehicleDetailsPage.OnAppearing: {ex.Message}");
            }
        }
        
        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            
            try
            {
                // If needed, clean up any resources when leaving the page
                ViewModel?.CleanupCommand?.Execute(null);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in VehicleDetailsPage.OnDisappearing: {ex.Message}");
            }
        }
    }
}
