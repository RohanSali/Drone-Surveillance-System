# 🚀 Quick Start Guide - Drone Surveillance System

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

---

**🎉 Ready to start? Pick Method 1, 2, or 3 above and follow the steps!**
