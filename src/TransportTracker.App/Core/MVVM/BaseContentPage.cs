using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using System;
using System.Threading.Tasks;

namespace TransportTracker.App.Core.MVVM
{
    /// <summary>
    /// Base page class for all content pages in the application.
    /// Provides automatic view model binding and lifecycle management.
    /// </summary>
    /// <typeparam name="TViewModel">The type of the view model associated with this page.</typeparam>
    public abstract class BaseContentPage<TViewModel> : ContentPage where TViewModel : BaseViewModel
    {
        private TViewModel _viewModel;
        private bool _isInitialized;

        /// <summary>
        /// Gets the view model associated with this page.
        /// </summary>
        public TViewModel ViewModel
        {
            get
            {
                if (_viewModel == null)
                {
                    // Resolve the view model from the service provider
                    _viewModel = Handler?.MauiContext?.Services?.GetService<TViewModel>();
                    
                    // If still null, try to create an instance using Activator
                    _viewModel ??= Activator.CreateInstance<TViewModel>();
                    
                    // Set the binding context to the view model
                    BindingContext = _viewModel;
                }
                
                return _viewModel;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseContentPage{TViewModel}"/> class.
        /// </summary>
        protected BaseContentPage()
        {
            // The actual view model initialization is delayed until the page appears
            // to ensure that the MauiContext and Handler are available
        }

        /// <summary>
        /// Called when the page appears.
        /// </summary>
        protected override async void OnAppearing()
        {
            base.OnAppearing();

            try
            {
                // Initialize the view model if it hasn't been initialized yet
                if (!_isInitialized)
                {
                    await ViewModel.InitializeAsync();
                    _isInitialized = true;
                }

                // Notify the view model that the page is appearing
                await ViewModel.OnAppearingAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to initialize: {ex.Message}", "OK");
            }
        }

        /// <summary>
        /// Called when the page disappears.
        /// </summary>
        protected override async void OnDisappearing()
        {
            base.OnDisappearing();

            // Notify the view model that the page is disappearing
            await ViewModel.OnDisappearingAsync();
        }
    }
}
