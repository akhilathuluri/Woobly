# Floating Island - Quick Start Guide

## Prerequisites
- Windows 10/11
- .NET 8.0 SDK (or Runtime)

## 5-Minute Setup

### Step 1: Get OpenWeather API Key (Developer/First-time Setup)
1. Go to https://openweathermap.org/api
2. Sign up for a free account
3. Generate an API key (free tier is sufficient)
4. Copy your API key

### Step 2: Configure API Key
1. Open `appsettings.json` in the project folder
2. Replace `YOUR_API_KEY_HERE` with your actual API key:
```json
{
  "OpenWeatherApiKey": "abc123your_actual_key_here"
}
```
3. Save the file

### Step 3: Build & Run
```powershell
# Navigate to project folder
cd e:\island\FloatingIsland

# Build the project
dotnet build

# Run the application
dotnet run
```

### Step 4: First Launch
- A small black bar will appear at the top center of your screen
- It shows: Time | Temperature | Battery%

### Step 5: Explore
1. **Click the island** - it expands
2. **Swipe left/right** - navigate between 6 pages
3. **Wait 3 seconds** - it auto-collapses

## Optional: Configure AI (For AI Features)
1. Expand the island (click it)
2. Swipe to the last page (Settings - Page 6)
3. Enter your OpenRouter API key (get from https://openrouter.ai)
4. Click "Save Settings"
5. Navigate to Page 3 (AI Response)
6. Type a message and press Enter

## Page Overview
- **Page 1**: System info (time, date, weather, battery)
- **Page 2**: Media player (shows what's playing)
- **Page 3**: AI chat (requires API key)
- **Page 4**: Quick tasks (type and press Enter)
- **Page 5**: Clipboard history (last 2 items)
- **Page 6**: Settings (AI & weather config)

## Keyboard Shortcuts
- **Enter** in AI input → Send message
- **Enter** in Task input → Add new task
- **Mouse swipe left/right** → Navigate pages

## Closing the App
Press `Ctrl+C` in the terminal where it's running, or:
1. Open Task Manager (Ctrl+Shift+Esc)
2. Find "FloatingIsland"
3. End task

## Tips
- The island auto-collapses after 3 seconds of no interaction
- All tasks are saved automatically
- Clipboard history updates in real-time
- Weather updates every 5 minutes
- The island stays on top of all windows

## Troubleshooting

**Problem: Island doesn't appear**
- Check Task Manager to see if it's running
- Verify .NET 8.0 is installed: `dotnet --version`
- Try running as administrator

**Problem: No weather data**
- Check your API key in appsettings.json
- Verify internet connection
- Wait 30 seconds for first weather update

**Problem: AI not working**
- Configure OpenRouter API key in Settings (Page 6)
- Ensure you have internet connection
- Check API key validity at openrouter.ai

## Creating a Shortcut
Create a batch file `start-island.bat`:
```batch
@echo off
cd /d e:\island\FloatingIsland
start "" dotnet run
```

Or publish as a standalone executable:
```powershell
dotnet publish -c Release -r win-x64 --self-contained
```

The executable will be at:
```
bin\Release\net8.0-windows\win-x64\publish\FloatingIsland.exe
```

## Next Steps
- Customize city in Settings
- Add your daily tasks
- Try the AI assistant
- Watch your clipboard history build up

Enjoy your new desktop companion! 🏝️
