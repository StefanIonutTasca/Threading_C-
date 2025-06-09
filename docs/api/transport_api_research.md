# Transport API Integration Documentation

## Available Public Transport APIs

### 1. OpenTransport API
- **Base URL**: https://api.opentransport.com/v1
- **Authentication**: API Key (Header: X-API-Key)
- **Rate Limits**: 5000 requests per day, maximum 60 requests per minute
- **Data Format**: JSON

#### Key Endpoints:

##### Get Vehicles
```
GET /vehicles
Query parameters:
  - latitude: decimal (center point latitude)
  - longitude: decimal (center point longitude)
  - radius: integer (meters, default: 1000)
  - types: string (comma-separated: bus,train,tram)
```

##### Get Routes
```
GET /routes
Query parameters:
  - agency_id: string
  - active_only: boolean (default: true)
```

##### Get Stops
```
GET /stops
Query parameters:
  - route_id: string
  - latitude: decimal
  - longitude: decimal
  - radius: integer (meters)
```

#### Sample Response (Vehicles):
```json
{
  "vehicles": [
    {
      "id": "v12345",
      "type": "bus",
      "route_id": "r987",
      "position": {
        "latitude": 37.7749,
        "longitude": -122.4194
      },
      "heading": 90,
      "speed": 25,
      "status": "in_transit",
      "occupancy": "many_seats_available",
      "timestamp": "2025-06-09T10:00:00Z"
    }
  ],
  "meta": {
    "count": 1,
    "total": 245
  }
}
```

### 2. CityTransit API
- **Base URL**: https://citytransit.api.com/v2
- **Authentication**: OAuth 2.0
- **Rate Limits**: 10,000 requests per day
- **Data Format**: JSON

#### Key Endpoints:

##### Get Vehicle Locations
```
GET /transit/vehicles/locations
Query parameters:
  - bounds: string (lat1,lon1,lat2,lon2)
  - route_ids: string (comma-separated IDs)
  - updated_since: timestamp
```

##### Get Arrival Predictions
```
GET /transit/stops/predictions
Query parameters:
  - stop_id: string
  - route_ids: string (comma-separated)
  - limit: integer (default: 10)
```

#### Sample Response (Predictions):
```json
{
  "predictions": [
    {
      "route_id": "red_line",
      "stop_id": "central_station",
      "vehicle_id": "train_842",
      "arrival_time": "2025-06-09T10:15:30Z",
      "is_live": true,
      "delay_seconds": 45
    }
  ]
}
```

## Data Volume Considerations
- Average response size: ~10KB per 20 vehicles
- For 100,000+ records: Implement paging and efficient caching
- Consider using ETags for conditional requests
- Batch processing recommended for historical data analysis

## API Evaluation Matrix

| Feature | OpenTransport API | CityTransit API |
|---------|------------------|----------------|
| Coverage | 50+ cities | 25 major cities |
| Real-time accuracy | ±30 seconds | ±45 seconds |
| Update frequency | 15-30 seconds | 30-60 seconds |
| Historical data | 7 days | 30 days |
| Developer support | Documentation + Forum | Documentation + SDK |
| Cost | Free tier available | Free tier limited |

## Implementation Recommendations
1. Use HttpClientFactory for connection pooling
2. Implement retry policies with exponential backoff
3. Set up a background service for polling at 30-second intervals
4. Use PLINQ for processing large response datasets
5. Implement a fallback mechanism between APIs for increased reliability
