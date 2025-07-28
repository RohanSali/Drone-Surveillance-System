# 🚀 Quick Start Guide - Drone Surveillance System

## 🆕 Latest Updates (2024)

### New Features Added:
- ✅ **Casualty & Anomaly Classifiers**: Advanced AI models for detection
- ✅ **Real-time Drone Tracking**: Live position monitoring with GPS
- ✅ **FastAPI Backend**: RESTful API server for drone communication
- ✅ **Enhanced UI**: Monitoring & Alerts, Radar View, Module Selector, Image Viewer
- ✅ **Multi-drone Support**: Track and control multiple drones simultaneously
- ✅ **MQTT Integration**: Real-time communication protocol (configurable)

### AI Models Available:
- **FaceNet Recognition**: Face detection and recognition
- **Crowd Detection**: YOLOv8-based crowd monitoring (Roboflow integration)
- **Casualty Classifier**: Accident and emergency detection
- **Anomaly Classifier**: Unusual behavior pattern detection

## ⚡ Fastest Way to Run (Choose One Method)

### Method 1: Install .NET SDK (Recommended)

1. **Download .NET 8 SDK:**
   - Go to: https://dotnet.microsoft.com/en-us/download/dotnet/8.0
   - Download "SDK x64" for Windows
   - Run the installer and follow the prompts

2. **Restart your terminal/PowerShell**

3. **Navigate to project folder:**
   ```powershell
   cd "C:\Users\91880\Downloads\Surviellance"
   ```

4. **Run the application:**
   ```powershell
   dotnet restore
   dotnet run
   ```

### Method 2: Use Visual Studio Community (Free)

1. **Download Visual Studio Community 2022:**
   - Go to: https://visualstudio.microsoft.com/vs/community/
   - Download and install (it includes .NET SDK)
   - During installation, select ".NET desktop development" workload

2. **Open the project:**
   - Launch Visual Studio
   - Click "Open a project or solution"
   - Navigate to: `C:\Users\91880\Downloads\Surviellance\DroneSurveillanceSystem.csproj`
   - Click Open

3. **Run the application:**
   - Press `F5` or click the green "Start" button
   - Visual Studio will automatically restore packages and build

### Method 3: Use Visual Studio Code (Lightweight)

1. **Download VS Code:**
   - Go to: https://code.visualstudio.com/
   - Download and install

2. **Install .NET SDK** (same as Method 1 step 1)

3. **Install C# Extension:**
   - Open VS Code
   - Go to Extensions (Ctrl+Shift+X)
   - Search for "C#" by Microsoft
   - Install it

4. **Open project:**
   - File → Open Folder
   - Select: `C:\Users\91880\Downloads\Surviellance`

5. **Run from terminal:**
   - Open terminal in VS Code (Ctrl+`)
   - Run: `dotnet restore && dotnet run`

## 🎯 What You'll See When It Runs

1. **Main Dashboard Window** opens (1200x800 pixels)
2. **Dark theme interface** with:
   - Live surveillance feed area (left side)
   - Detection metrics panel (right side)
   - Activity log at the bottom-right
   - Control buttons at the bottom

3. **Key Features to Try:**
   - Click **"Start Detection"** to begin simulation
   - Watch **real-time alerts** appear randomly
   - Check the **Activity Log** for timestamped events
   - Click **"Settings"** to configure the system
   - Use **"Load New Scene"** to add custom images

## 🛠️ If You Get Errors

### "dotnet command not found"
- You need to install .NET 8 SDK (Method 1 above)
- After installation, close and reopen your terminal

### "Package restore failed"
- Check your internet connection
- Try running: `dotnet nuget locals all --clear`
- Then: `dotnet restore`

### "Build failed"
- Make sure you're in the correct directory: `C:\Users\91880\Downloads\Surviellance`
- Verify all files are present (check README.md for file list)

### Application crashes on startup
- Run PowerShell as Administrator
- Try: `dotnet run --verbosity detailed` to see detailed errors

## 📱 Quick Demo Steps

Once the app starts:

1. **Click "Start Detection"** - The AI status light turns green
2. **Wait 10-15 seconds** - You'll see random crowd detection alerts
3. **Check the Activity Log** - New entries appear with timestamps
4. **Click "Settings"** - Explore configuration options
5. **Try "Load New Scene"** - Add your own surveillance images
6. **Watch the GPS coordinates** - They update as the "drone" moves

## 🔧 System Requirements

- **OS:** Windows 10 version 1903 or higher, Windows 11
- **Memory:** 4 GB RAM minimum
- **Storage:** 500 MB free space
- **Display:** 1024x768 minimum resolution (1920x1080 recommended)
- **.NET:** Version 8.0 or higher

## 📞 Need Help?

If you encounter issues:

1. **Check Windows Updates** - Make sure Windows is up to date
2. **Run as Administrator** - Right-click PowerShell → "Run as administrator"
3. **Check the README.md** - For detailed troubleshooting
4. **Windows Event Viewer** - Look for application errors if it crashes

## 🌐 API Backend Server (Optional)

The system includes a FastAPI backend server for drone communication and data management.

### Start the API Server:

1. **Navigate to Server directory:**
   ```powershell
   cd "C:\Users\91880\Downloads\Surviellance\Drone-Surveillance-System\Server"
   ```

2. **Install Python dependencies:**
   ```powershell
   pip install fastapi uvicorn sqlalchemy
   ```

3. **Start the server:**
   ```powershell
   python main.py
   ```
   
   Server will be available at: `http://localhost:8000`
   API Documentation: `http://localhost:8000/docs`

### API Endpoints Available:

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/v1/groups/create/` | Create a drone group for operations |
| POST | `/api/v1/drones/register/` | Register a new drone to the system |
| POST | `/api/v1/drones/data/` | Upload surveillance data from drone |
| POST | `/api/v1/drones/control/` | Send control commands to drone groups |

### API Usage Examples:

**Create a Group:**
```bash
curl -X POST "http://localhost:8000/api/v1/groups/create/" \
     -F "region=urban_zone" \
     -F "purpose=casualty_detection" \
     -F "rl_model_instance=model_v1"
```

**Register a Drone:**
```bash
curl -X POST "http://localhost:8000/api/v1/drones/register/" \
     -F "drone_id=101" \
     -F "location=40.7128,-74.0060" \
     -F "purpose=casualty_detection"
```

**Upload Data:**
```bash
curl -X POST "http://localhost:8000/api/v1/drones/data/" \
     -F "drone_id=101" \
     -F "image=@surveillance_image.jpg" \
     -F "location=40.7128,-74.0060" \
     -F "score=0.85"
```

## 🤖 AI Models & Notebooks

The system includes pre-trained AI models and Jupyter notebooks for development:

### Available Models:
- `casualty_classifier.h5` - Detects accidents and emergencies
- `anamoly_classifier.h5` - Identifies unusual behavior patterns
- `best_model_fold_1.h5` - Face recognition model

### Jupyter Notebooks:
- `FaceNet_Recognition.ipynb` - Face detection and recognition training
- `Roboflow_Crowd_Detection.ipynb` - Crowd detection with YOLOv8

### To Use Notebooks:
1. Install Jupyter: `pip install jupyter`
2. Navigate to Notebooks folder: `cd "..\Notebooks"`
3. Start Jupyter: `jupyter notebook`
4. Open the desired `.ipynb` file

## 🗄️ Database & Data Storage

### Database Locations:
- **SQLite Database**: `%APPDATA%\DroneSurveillance\surveillance.db`
- **Detection Logs**: `%APPDATA%\DroneSurveillance\detection_log.json`
- **Settings**: `%APPDATA%\DroneSurveillance\settings.json`
- **Server Database**: `Server\drone_surveillance.db`

### Database Schema:
- **Groups**: Store drone operation groups by region and purpose
- **Drones**: Individual drone registration and location data
- **DataLogs**: Surveillance data uploads with scores and timestamps

## 🎮 New UI Features

The application now includes several new interactive windows:

### 📊 Monitoring & Alerts Page
- Network selection dropdown (NW1, NW2, etc.)
- Real-time drone position display
- Interactive alert cards
- Click "📊 Monitoring" from main dashboard

### 🎯 Radar View
- Animated radar sweep with drone tracking
- Live feed panel with image slider (1-100)
- Recording controls (Play, Pause, Stop)
- Click "🎯 Radar" from main dashboard

### 🔧 Module Selector
- AI Detection Module (🤖)
- Camera Control Module (📷)
- GPS Navigation Module (🗺️)
- Emergency Response Module (🚨)
- Click "🔧 Modules" from main dashboard

### 🖼️ Image Viewer
- Full-screen image viewing with zoom
- 90-degree rotation capability
- Timeline scrubbing (1-150 frames)
- Playback speed controls (0.5x to 4x)
- Click "🖼️ Viewer" from main dashboard

## 🔄 Real-time Features

### Drone Tracking System:
- **Live Position Updates**: GPS coordinates with real-time tracking
- **Status Monitoring**: Battery level, signal strength, speed
- **Alert System**: Low battery, weak signal, boundary violations
- **Multi-drone Support**: Track multiple drones simultaneously
- **Historical Data**: Flight path visualization and position history

### Data Processing:
- **Event-driven Architecture**: Real-time UI updates
- **Async/Await Operations**: Non-blocking database operations
- **Timer-based Updates**: Configurable refresh intervals
- **MQTT Integration**: Real-time communication (when enabled)

---

**🎉 Ready to start? Pick Method 1, 2, or 3 above and follow the steps!**

*For advanced users: Start both the .NET application and the FastAPI server for full functionality.*
