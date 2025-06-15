using TransportTracker.App.Core.MVVM;
using TransportTracker.App.ViewModels;

namespace TransportTracker.App.Views.Vehicles
{
    /// <summary>
    /// View for displaying and interacting with a list of transport vehicles.
    /// </summary>
    public partial class VehiclesView : BaseContentPage<VehiclesViewModel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="VehiclesView"/> class.
        /// </summary>
        public VehiclesView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Called when the sort option is changed.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The event arguments.</param>
        private void OnSortOptionChanged(object sender, EventArgs e)
        {
            if (BindingContext is VehiclesViewModel viewModel)
            {
                viewModel.ApplySortCommand.Execute(null);
            }
        }
    }
}
