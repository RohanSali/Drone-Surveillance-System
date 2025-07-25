@echo off
title Drone Surveillance System Launcher
color 0A

echo.
echo  ===================================================
echo   🚁 DRONE SURVEILLANCE AI SYSTEM LAUNCHER 🚁
echo  ===================================================
echo.

REM Check if .NET is installed
dotnet --version >nul 2>&1
if %errorlevel% neq 0 (
    echo  ❌ ERROR: .NET SDK is not installed!
    echo.
    echo  📥 Please install .NET 8 SDK first:
    echo     1. Go to: https://dotnet.microsoft.com/download/dotnet/8.0
    echo     2. Download "SDK x64" for Windows
    echo     3. Run the installer
    echo     4. Restart this script
    echo.
    pause
    exit /b 1
)

echo  ✅ .NET SDK detected: 
dotnet --version
echo.

echo  🔄 Restoring packages...
dotnet restore DroneSurveillanceSystem.csproj
if %errorlevel% neq 0 (
    echo  ❌ Package restore failed!
    echo     - Check your internet connection
    echo     - Try running as Administrator
    pause
    exit /b 1
)

echo  ✅ Packages restored successfully!
echo.

echo  🔨 Building application...
dotnet build DroneSurveillanceSystem.csproj --configuration Release
if %errorlevel% neq 0 (
    echo  ❌ Build failed!
    echo     - Check the error messages above
    echo     - Try running as Administrator
    pause
    exit /b 1
)

echo  ✅ Build successful!
echo.

echo  🚀 Starting Drone Surveillance System...
echo     - Main dashboard will open shortly
echo     - Click "Start Detection" to begin simulation
echo     - Use "Settings" to configure the system
echo.

dotnet run --project DroneSurveillanceSystem.csproj
if %errorlevel% neq 0 (
    echo.
    echo  ❌ Application failed to start!
    echo     - Check Windows Event Viewer for details
    echo     - Try running as Administrator
    pause
    exit /b 1
)

echo.
echo  👋 Surveillance system closed.
pause
