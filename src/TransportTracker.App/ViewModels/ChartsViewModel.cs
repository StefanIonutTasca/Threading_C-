using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Graphics;
using TransportTracker.App.Core.Charts;
using TransportTracker.App.Core.MVVM;
using TransportTracker.App.Services;

namespace TransportTracker.App.ViewModels
{
    /// <summary>
    /// View model that provides data and commands for chart visualizations.
    /// </summary>
    public class ChartsViewModel : BaseViewModel
    {
        private ObservableCollection<ChartEntry> _arrivalPredictions;
        private ObservableCollection<ChartEntry> _routeFrequency;
        private ObservableCollection<ChartEntry> _capacityOccupancy;
        private ObservableCollection<ChartEntry> _delayStatistics;
        private string _selectedRouteId;
        private string _selectedStopId;
        private int _timeRangeHours = 2;

        /// <summary>
        /// Initializes a new instance of the <see cref="ChartsViewModel"/> class.
        /// </summary>
        /// <param name="navigationService">The navigation service.</param>
        public ChartsViewModel(INavigationService navigationService)
        {
            Title = "Transport Charts";
            Icon = "chart_icon.png";

            // Initialize commands
            RefreshChartsCommand = CreateAsyncCommand(RefreshAllChartsAsync);
            ChangeTimeRangeCommand = CreateCommand<int>(ChangeTimeRange);
            SelectRouteCommand = CreateCommand<string>(SelectRoute);
            SelectStopCommand = CreateCommand<string>(SelectStop);

            // Initialize collections
            ArrivalPredictions = new ObservableCollection<ChartEntry>();
            RouteFrequency = new ObservableCollection<ChartEntry>();
            CapacityOccupancy = new ObservableCollection<ChartEntry>();
            DelayStatistics = new ObservableCollection<ChartEntry>();
        }

        /// <summary>
        /// Gets or sets the arrival predictions entries.
        /// </summary>
        public ObservableCollection<ChartEntry> ArrivalPredictions
        {
            get => _arrivalPredictions;
            set => SetProperty(ref _arrivalPredictions, value);
        }

        /// <summary>
        /// Gets or sets the route frequency entries.
        /// </summary>
        public ObservableCollection<ChartEntry> RouteFrequency
        {
            get => _routeFrequency;
            set => SetProperty(ref _routeFrequency, value);
        }

        /// <summary>
        /// Gets or sets the capacity and occupancy entries.
        /// </summary>
        public ObservableCollection<ChartEntry> CapacityOccupancy
        {
            get => _capacityOccupancy;
            set => SetProperty(ref _capacityOccupancy, value);
        }

        /// <summary>
        /// Gets or sets the delay statistics entries.
        /// </summary>
        public ObservableCollection<ChartEntry> DelayStatistics
        {
            get => _delayStatistics;
            set => SetProperty(ref _delayStatistics, value);
        }

        /// <summary>
        /// Gets or sets the selected route ID.
        /// </summary>
        public string SelectedRouteId
        {
            get => _selectedRouteId;
            set => SetProperty(ref _selectedRouteId, value);
        }

        /// <summary>
        /// Gets or sets the selected stop ID.
        /// </summary>
        public string SelectedStopId
        {
            get => _selectedStopId;
            set => SetProperty(ref _selectedStopId, value);
        }

        /// <summary>
        /// Gets or sets the time range in hours.
        /// </summary>
        public int TimeRangeHours
        {
            get => _timeRangeHours;
            set => SetProperty(ref _timeRangeHours, value);
        }

        /// <summary>
        /// Gets the command to refresh all charts.
        /// </summary>
        public ICommand RefreshChartsCommand { get; }

        /// <summary>
        /// Gets the command to change the time range.
        /// </summary>
        public ICommand ChangeTimeRangeCommand { get; }

        /// <summary>
        /// Gets the command to select a route.
        /// </summary>
        public ICommand SelectRouteCommand { get; }

        /// <summary>
        /// Gets the command to select a stop.
        /// </summary>
        public ICommand SelectStopCommand { get; }

        /// <summary>
        /// Initializes the view model asynchronously.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public override async Task InitializeAsync()
        {
            if (IsInitialized)
                return;

            await RefreshAllChartsAsync();
            IsInitialized = true;
        }

        private async Task RefreshAllChartsAsync()
        {
            if (IsBusy)
                return;

            try
            {
                IsBusy = true;

                // Generate mock data for all charts
                GenerateArrivalPredictionData();
                GenerateRouteFrequencyData();
                GenerateCapacityOccupancyData();
                GenerateDelayStatisticsData();

                // Simulate network delay
                await Task.Delay(800);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ChangeTimeRange(int hours)
        {
            if (hours > 0 && hours <= 24)
            {
                TimeRangeHours = hours;
                RefreshChartsCommand.Execute(null);
            }
        }

        private void SelectRoute(string routeId)
        {
            SelectedRouteId = routeId;
            RefreshChartsCommand.Execute(null);
        }

        private void SelectStop(string stopId)
        {
            SelectedStopId = stopId;
            RefreshChartsCommand.Execute(null);
        }

        private void GenerateArrivalPredictionData()
        {
            var random = new Random();
            var now = DateTime.Now;
            var entries = new List<ChartEntry>();

            // Generate predictions for the next TimeRangeHours hours
            for (int minutes = 5; minutes <= TimeRangeHours * 60; minutes += 5)
            {
                // Create a prediction entry (minutes until arrival)
                var prediction = minutes;
                
                // Add some randomness for realistic data
                var jitter = random.Next(-2, 3);
                prediction += jitter;
                
                if (prediction < 0) prediction = 0;
                
                var entry = new ChartEntry(prediction)
                {
                    Label = now.AddMinutes(minutes).ToString("HH:mm"),
                    Color = minutes < 15 ? Colors.Red : (minutes < 30 ? Colors.Orange : Colors.Green)
                };
                
                // Highlight the next arrival
                if (minutes == 5)
                {
                    entry.IsHighlighted = true;
                }
                
                entries.Add(entry);
            }

            ArrivalPredictions = new ObservableCollection<ChartEntry>(entries);
        }

        private void GenerateRouteFrequencyData()
        {
            var random = new Random();
            var entries = new List<ChartEntry>();
            
            // Generate data for a 24-hour period
            for (int hour = 0; hour < 24; hour++)
            {
                // Create frequency entry (buses per hour)
                float frequency;
                
                if (hour >= 6 && hour <= 9)
                {
                    // Morning rush hour
                    frequency = random.Next(8, 15);
                }
                else if (hour >= 16 && hour <= 19)
                {
                    // Evening rush hour
                    frequency = random.Next(7, 14);
                }
                else if (hour >= 22 || hour < 5)
                {
                    // Night hours
                    frequency = random.Next(0, 4);
                }
                else
                {
                    // Regular daytime
                    frequency = random.Next(3, 8);
                }
                
                var entry = new ChartEntry(frequency)
                {
                    Label = $"{hour:D2}:00",
                    Color = Colors.Blue
                };
                
                // Highlight current hour
                if (hour == DateTime.Now.Hour)
                {
                    entry.IsHighlighted = true;
                }
                
                entries.Add(entry);
            }

            RouteFrequency = new ObservableCollection<ChartEntry>(entries);
        }

        private void GenerateCapacityOccupancyData()
        {
            var random = new Random();
            var entries = new List<ChartEntry>();
            
            // Generate hourly data for occupancy percentage
            for (int hour = 0; hour < 24; hour++)
            {
                // Calculate occupancy percentage
                float occupancyPercentage;
                
                if (hour >= 6 && hour <= 9)
                {
                    // Morning rush hour
                    occupancyPercentage = random.Next(70, 101);
                }
                else if (hour >= 16 && hour <= 19)
                {
                    // Evening rush hour
                    occupancyPercentage = random.Next(65, 96);
                }
                else if (hour >= 22 || hour < 5)
                {
                    // Night hours
                    occupancyPercentage = random.Next(10, 40);
                }
                else
                {
                    // Regular daytime
                    occupancyPercentage = random.Next(30, 71);
                }
                
                var entry = new ChartEntry(occupancyPercentage)
                {
                    Label = $"{hour:D2}:00",
                    Color = Colors.Purple
                };
                
                entries.Add(entry);
            }

            CapacityOccupancy = new ObservableCollection<ChartEntry>(entries);
        }

        private void GenerateDelayStatisticsData()
        {
            var random = new Random();
            var entries = new List<ChartEntry>();
            string[] daysOfWeek = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
            
            // Generate average delay minutes per day of week
            for (int day = 0; day < 7; day++)
            {
                // Create delay entry (average minutes of delay)
                float delay;
                
                if (day < 5) // Weekdays
                {
                    delay = random.Next(2, 8);
                }
                else // Weekend
                {
                    delay = random.Next(1, 4);
                }
                
                // Add some randomization for Friday
                if (day == 4) // Friday
                {
                    delay += random.Next(1, 4);
                }
                
                var entry = new ChartEntry(delay)
                {
                    Label = daysOfWeek[day],
                    Color = Colors.Orange
                };
                
                // Highlight current day
                if (day == (int)DateTime.Now.DayOfWeek - 1)
                {
                    entry.IsHighlighted = true;
                }
                
                entries.Add(entry);
            }

            DelayStatistics = new ObservableCollection<ChartEntry>(entries);
        }
    }
}
