using Android.Content;
using Android.Gms.Maps.Model;
using Android.Graphics;
using Android.Graphics.Drawables;
using Microsoft.Maui.Controls.Compatibility.Platform.Android;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps.Handlers;
using Microsoft.Maui.Platform;
using TransportTracker.App.Views.Maps;
using Color = Microsoft.Maui.Graphics.Color;

namespace TransportTracker.App.Platforms.Android.Renderers
{
    public class CustomMapPinRenderer : MapHandler
    {
        private Dictionary<string, BitmapDescriptor> _typeIcons = new Dictionary<string, BitmapDescriptor>();

        protected override void ConnectHandler(MapView platformView)
        {
            base.ConnectHandler(platformView);

            if (platformView.Map != null)
            {
                platformView.Map.MarkerClick += OnMarkerClick;
                platformView.Map.InfoWindowClick += OnInfoWindowClick;

                // Initialize pin icons for different transport types
                PreloadVehicleIcons(Context);
            }
        }

        private void PreloadVehicleIcons(Context context)
        {
            // Define colors for different transport types
            var transportTypes = new Dictionary<string, Color>
            {
                { "Bus", Color.FromHex("#E63946") },
                { "Train", Color.FromHex("#4361EE") },
                { "Tram", Color.FromHex("#06D6A0") },
                { "Subway", Color.FromHex("#FFD166") },
                { "Ferry", Color.FromHex("#118AB2") },
                // Default color for unknown types
                { "Default", Color.FromHex("#999999") }
            };

            foreach (var type in transportTypes)
            {
                _typeIcons[type.Key] = CreateVehicleMarkerIcon(context, type.Value, type.Key);
            }
        }

        private BitmapDescriptor CreateVehicleMarkerIcon(Context context, Color color, string vehicleType)
        {
            // Create a drawable for the pin
            GradientDrawable shape = new GradientDrawable();
            shape.SetShape(ShapeType.Oval);
            shape.SetColor(color.ToAndroid());
            shape.SetStroke(3, global::Android.Graphics.Color.White);

            // Convert the drawable to a bitmap
            Bitmap bitmap = Bitmap.CreateBitmap(48, 48, Bitmap.Config.Argb8888);
            Canvas canvas = new Canvas(bitmap);
            shape.SetBounds(0, 0, 48, 48);
            shape.Draw(canvas);

            // Add a text label (first letter of vehicle type)
            Paint textPaint = new Paint();
            textPaint.Color = global::Android.Graphics.Color.White;
            textPaint.TextSize = 24;
            textPaint.TextAlign = Paint.Align.Center;
            textPaint.AntiAlias = true;
            textPaint.FakeBoldText = true;

            // Draw the first letter of the vehicle type
            canvas.DrawText(
                vehicleType.Substring(0, 1), 
                bitmap.Width / 2,
                (bitmap.Height / 2) + 8, // Offset a bit to center vertically
                textPaint);

            return BitmapDescriptorFactory.FromBitmap(bitmap);
        }

        protected override void DisconnectHandler(MapView platformView)
        {
            if (platformView.Map != null)
            {
                platformView.Map.MarkerClick -= OnMarkerClick;
                platformView.Map.InfoWindowClick -= OnInfoWindowClick;
            }

            base.DisconnectHandler(platformView);
        }

        private void OnMarkerClick(object sender, GoogleMap.MarkerClickEventArgs e)
        {
            // Handle marker click
            e.Handled = false;
        }

        private void OnInfoWindowClick(object sender, GoogleMap.InfoWindowClickEventArgs e)
        {
            // Handle info window click
        }

        protected override void AddPins(MapView mapView, IEnumerable<IMapPin> mapPins)
        {
            if (mapView?.Map == null)
                return;

            foreach (var pin in mapPins)
            {
                var marker = new MarkerOptions();
                marker.SetPosition(new LatLng(pin.Location.Latitude, pin.Location.Longitude));
                marker.SetTitle(pin.Label);
                marker.SetSnippet(pin.Address);

                // Use custom icon based on vehicle type
                if (pin.BindingContext is TransportVehicle vehicle)
                {
                    // Use the vehicle type to select an icon
                    if (_typeIcons.TryGetValue(vehicle.Type, out var icon))
                    {
                        marker.SetIcon(icon);
                    }
                    else
                    {
                        // Use default icon
                        marker.SetIcon(_typeIcons["Default"]);
                    }
                }

                mapView.Map.AddMarker(marker);
            }
        }
    }
}
