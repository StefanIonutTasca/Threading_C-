# Real-Time Public Transport Tracker

## Project Overview
This application is a multi-threaded .NET MAUI application that demonstrates advanced threading concepts through a practical public transport tracking system. The application allows users to track public transport routes, monitor vehicles in real-time, and process large datasets efficiently using parallel processing techniques.

## Requirements

- .NET 8.0 SDK or higher
- Visual Studio 2022 or Visual Studio Code with .NET MAUI workload
- Windows 10/11 (for Windows builds) or macOS (for iOS/macOS builds)
- Internet connection for API data (fallback to sample data available)

## How to Run the Project

### Using Visual Studio 2022

1. **Open the Solution**:
   - Open Visual Studio 2022
   - Select "Open a project or solution"
   - Navigate to the project folder and open `ThreadingCS.sln`

2. **Select Target Platform**:
   - Choose your target platform from the dropdown menu (Windows, Android, iOS, or macOS)
   - For Windows, select "Windows Machine" as the deployment target

3. **Build and Run**:
   - Click the "Start Debugging" button (green play button) or press F5
   - The application will build and launch on your selected platform

### Using Command Line

1. **Navigate to Project Directory**:
   ```
   cd c:\Users\stefa\Desktop\School\Programare\Threading_C-\ThreadingCS
   ```

2. **Build the Project**:
   ```
   dotnet build
   ```

3. **Run the Project** (Windows example):
   ```
   dotnet run --framework net8.0-windows10.0.19041.0
   ```

## Using the Application

1. **Set Destination Coordinates**:
   - Enter latitude and longitude values for your destination
   - Default values are provided (51.505983, -0.017931)

2. **Load Data**:
   - Click "Load Data" to fetch transport routes from the API
   - The application will fall back to sample data if the API is unavailable

3. **Start Monitoring**:
   - Click "Start Monitoring" to begin real-time updates of vehicle positions
   - Click again to stop monitoring

4. **Process Large Dataset**:
   - Click "Process Large Dataset" to demonstrate PLINQ processing of 100,000+ records
   - Watch the progress bar and status updates as processing occurs

5. **Apply Filters**:
   - Use the search box to filter routes by name
   - Adjust the sliders to filter by maximum duration and distance

## Threading Features Demonstrated

1. **Task-based Asynchronous Pattern**:
   - All network and database operations use async/await
   - UI remains responsive during long-running operations
   - Proper timeout handling with CancellationTokenSource

2. **Parallel LINQ (PLINQ)**:
   - Large dataset processing using AsParallel() for efficient multi-core utilization
   - WithDegreeOfParallelism to optimize for available CPU cores
   - Batch processing to handle 100,000+ records efficiently

3. **Thread Synchronization**:
   - Safe UI updates using MainThread.BeginInvokeOnMainThread
   - Proper cancellation token usage for stoppable operations
   - Thread-safe collections for concurrent access

4. **Concurrent Tasks**:
   - Multiple parallel API calls using Task.WhenAll
   - Background monitoring with cancellation support
   - Asynchronous data visualization rendering

## Notes for Evaluation

This project demonstrates advanced multi-threading concepts with significant added value:

### Multi-threading
- PLINQ with 100,000+ records database processing
- Multiple asynchronous I/O calls to API with parallel execution
- Data visualization using plots/graphs and thread pool
- Batch processing using tasks of the thread pool
- Thread-safe UI updates with MainThread dispatcher
- Proper cancellation handling throughout the application

### User Interface
- Clean and responsive MAUI interface with proper styling
- Real-time progress indicators and status messages
- Interactive map visualization with vehicle tracking
- Dynamic charts and graphs for data visualization

### Code Quality
- Clean architecture with separation of concerns (MVVM pattern)
- Comprehensive error handling and timeout management
- Optimized database operations with batch processing
- Thread-safe collections and operations

### Code Conventions and Comments
- Consistent naming conventions following C# standards
- Detailed comments explaining complex threading operations
- Debug logging for troubleshooting and monitoring
- XML documentation on key methods and classes