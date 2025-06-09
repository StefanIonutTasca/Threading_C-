# Real-Time Public Transport Tracker

A multi-threaded .NET MAUI application that provides real-time tracking of public transportation.

## Features

- Live bus/train tracking with real-time map visualization
- Multi-threaded architecture for responsive user experience
- PLINQ-powered data processing for large datasets (100k+ entries)
- Async API integration with various public transport providers
- Cross-platform support via .NET MAUI (iOS, Android, Windows)

## Technical Overview

This application demonstrates advanced threading concepts including:

- Task Parallel Library for concurrent operations
- Async/Await pattern for non-blocking API calls
- Thread synchronization with various primitives
- Thread-safe collections for concurrent data access
- PLINQ for efficient parallel data processing

## Development Setup

### Prerequisites

- Visual Studio 2022 or later
- .NET 8.0 SDK
- Git

### Getting Started

1. Clone the repository
2. Open the solution in Visual Studio
3. Build and run the TransportTracker.App project

## Architecture

The solution consists of three main projects:

- **TransportTracker.App**: .NET MAUI user interface
- **TransportTracker.Core**: Business logic, threading infrastructure, API clients
- **TransportTracker.Tests**: Unit and integration tests

## Contributors

- Developer A: Core infrastructure, threading, API clients, data processing
- Developer B: UI components, visualization, MVVM implementation, maps
