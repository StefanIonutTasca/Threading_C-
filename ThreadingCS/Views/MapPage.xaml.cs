using Microsoft.Maui.Graphics;
using ThreadingCS.ViewModels;

namespace ThreadingCS.Views
{
    public partial class MapPage : ContentPage
    {
        private MapViewModel _viewModel;
        private IDrawable _vehiclesDrawable;

        public MapPage()
        {
            InitializeComponent();
            
            _viewModel = new MapViewModel();
            BindingContext = _viewModel;

            // Create the drawable for the vehicles
            _vehiclesDrawable = new VehiclesDrawable(_viewModel);
            VehiclesCanvas.Drawable = _vehiclesDrawable;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            if (_viewModel != null)
            {
                // Initialize the simulated map
                _viewModel.InitializeSimulatedMap();
                
                // Create the road network
                CreateRoadNetwork();
            }
            
            // Start updates
            _viewModel.StartLiveUpdates();
        }

        private void CreateRoadNetwork()
        {
            // Create a simulated road network
            RoadGrid.Children.Clear();
            RoadGrid.RowDefinitions.Clear();
            RoadGrid.ColumnDefinitions.Clear();
            
            // Create a grid layout for roads
            for (int i = 0; i < 10; i++)
            {
                RoadGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
                RoadGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            }

            // Create horizontal roads
            for (int i = 2; i < 10; i += 3)
            {
                var horizontalRoad = new BoxView
                {
                    Color = Colors.Gray,
                    HeightRequest = 10
                };
                Grid.SetRow(horizontalRoad, i);
                Grid.SetColumn(horizontalRoad, 0);
                Grid.SetColumnSpan(horizontalRoad, 10);
                RoadGrid.Children.Add(horizontalRoad);
            }

            // Create vertical roads
            for (int i = 2; i < 10; i += 3)
            {
                var verticalRoad = new BoxView
                {
                    Color = Colors.Gray,
                    WidthRequest = 10
                };
                Grid.SetColumn(verticalRoad, i);
                Grid.SetRow(verticalRoad, 0);
                Grid.SetRowSpan(verticalRoad, 10);
                RoadGrid.Children.Add(verticalRoad);
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _viewModel?.StopLiveUpdates();
        }
    }

    // Drawable for rendering vehicles on the canvas
    public class VehiclesDrawable : IDrawable
    {
        private readonly MapViewModel _viewModel;

        public VehiclesDrawable(MapViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            if (_viewModel == null) return;

            // Draw each vehicle
            foreach (var vehicle in _viewModel.GetVehiclePositions())
            {
                // Draw a circle representing the vehicle
                canvas.FillColor = vehicle.IsHighlighted ? Colors.Red : Colors.Blue;
                canvas.FillCircle(vehicle.X, vehicle.Y, 12);

                // Draw vehicle ID
                canvas.FontColor = Colors.White;
                canvas.FontSize = 8;
                canvas.DrawString(vehicle.Id, vehicle.X - 5, vehicle.Y + 3, HorizontalAlignment.Center);
            }
            
            // Force redraw at next frame
            MainThread.BeginInvokeOnMainThread(() => 
            {
                // Only request redraw if vehicles are actively updating
                if (_viewModel.IsLiveUpdateRunning)
                    Application.Current.MainPage?.FindByName<GraphicsView>("VehiclesCanvas")?.Invalidate();
            });
        }
    }
}
