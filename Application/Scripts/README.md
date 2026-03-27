# Coordinate Processing Script

## Overview
This directory contains the Python script that processes alert coordinates received from the server.

## Script Location
**Place your Python script here:** `Scripts/process_coordinates.py`

The application expects the script to be located at:
```
Application/Scripts/process_coordinates.py
```

## Script Requirements

### Input Format
The script receives JSON data via **stdin** with the following structure:
```json
{
    "latitude": 37.7749,
    "longitude": -122.4194,
    "altitude": 50.0,
    "alert_id": "alert_123",
    "drone_id": "drone_001",
    "timestamp": "2024-01-01T12:00:00.000Z"
}
```

### Output Format
The script must output JSON to **stdout** with the following structure:
```json
{
    "latitude": 37.7750,
    "longitude": -122.4195,
    "altitude": 50.5
}
```

### Example Script
See `process_coordinates.py.example` for a complete example template.

## How It Works

1. **Alert Reception**: When an alert with coordinates is received from the server via WebSocket, the coordinates are extracted.

2. **Coordinate Processing**: The `CoordinateProcessingService` calls your Python script with the coordinates as input.

3. **Result Sending**: The processed coordinates are automatically sent back to the server via WebSocket with the message type `processed_coordinates`.

## Message Format Sent to Server

The processed coordinates are sent to the server in this format:
```json
{
    "type": "processed_coordinates",
    "data": {
        "alert_id": "alert_123",
        "drone_id": "drone_001",
        "original_location": [37.7749, -122.4194, 50.0],
        "processed_location": [37.7750, -122.4195, 50.5],
        "timestamp": "2024-01-01T12:00:00.000Z"
    }
}
```

## Python Requirements

- Python 3.x must be installed on your system
- The script should be executable (on Linux/Mac: `chmod +x process_coordinates.py`)
- Required Python packages should be installed (if any)

## Testing Your Script

You can test your script manually:
```bash
echo '{"latitude": 37.7749, "longitude": -122.4194, "altitude": 50.0}' | python Scripts/process_coordinates.py
```

Expected output:
```json
{"latitude": 37.7750, "longitude": -122.4195, "altitude": 50.5}
```

## Troubleshooting

- **Script not found**: Make sure the script is named exactly `process_coordinates.py` and placed in the `Scripts` directory
- **Python not found**: Ensure Python is installed and accessible via `python` or `python3` command
- **Script errors**: Check the console output for error messages from the Python script
- **Timeout**: The script has a 10-second timeout. If processing takes longer, consider optimizing your script
