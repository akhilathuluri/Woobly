# Floating Island - Intelligent Desktop Companion

A Windows desktop companion inspired by Apple's Dynamic Island, providing a persistent floating interface with multiple interactive features.

## Features

- **Collapsed State**: Displays time, weather, and battery status
- **Expanded State**: Access 6 interactive pages by swiping
  - Page 1: System Overview (detailed time, date, weather, battery)
  - Page 2: Media Control (auto-detects playing media)
  - Page 3: AI Assistant (powered by OpenRouter)
  - Page 4: Task Memory (quick task management)
  - Page 5: Clipboard Memory (stores last 2 copied items)
  - Page 6: Settings

## System Requirements

- Windows 10/11
- .NET 8.0 Runtime or SDK

## Setup

### 1. Clone/Download the Project
```powershell
cd e:\island\FloatingIsland
```

### 2. Configure Weather API (Developer Setup)
- Get a free API key from [OpenWeather](https://openweathermap.org/api)
- Open [appsettings.json](appsettings.json)
- Replace `YOUR_API_KEY_HERE` with your actual API key:
```json
{
  "OpenWeatherApiKey": "your_openweather_api_key_here"
}
```

### 3. Build the Application
```powershell
dotnet build
```

### 4. Run the Application
```powershell
dotnet run
```

Or run the compiled executable:
```powershell
.\bin\Debug\net8.0-windows\FloatingIsland.exe
```

## Configuration

### Weather Settings
The OpenWeather API key is configured by the developer in [appsettings.json](appsettings.json). Users can change the city in Settings (Page 6).

### AI Settings (User Configuration)
Users must configure their own AI settings:
1. Click the island to expand
2. Swipe to Page 6 (Settings)
3. Enter your OpenRouter API key (get one from [OpenRouter](https://openrouter.ai))
4. Optionally change the AI model (default: anthropic/claude-3.5-sonnet)
5. Click "Save Settings"

## Usage

### Starting the Application
The island will appear at the top center of your screen as a small floating bar showing:
- Current time
- Weather temperature
- Battery percentage

### Interactions

**Expanding/Collapsing:**
- **Click the island** to expand it
- **Click outside** or **wait 3 seconds** of inactivity to auto-collapse
- Window **loses focus** → auto-collapse

**Page Navigation:**
- **Swipe left** to go to next page
- **Swipe right** to go to previous page
- **6 pages total** with navigation dots at the bottom

### Page Guide

**Page 1 - System Overview**
- Large clock display
- Current date
- Weather temperature and condition
- Battery percentage and charging status
- Updates automatically every second

**Page 2 - Media Context**
- Automatically detects playing media (Note: Full media integration pending)
- Shows title, artist, album
- Playback progress bar
- Current position and duration

**Page 3 - AI Response**
- Type your message in the text box
- Press **Enter** to send
- AI response appears below (user input is cleared)
- Clean, distraction-free AI interaction

**Page 4 - Task Memory**
- Type task and press **Enter** to add
- Click **checkbox** to mark complete/incomplete
- Click **×** button to remove task
- Tasks are saved locally and persist between sessions

**Page 5 - Clipboard Memory**
- Automatically captures your last 2 copied items
- Click any item to restore it to clipboard
- Shows preview (first 100 characters)
- Auto-updates when you copy new text

**Page 6 - Settings**
- Configure AI model and API key
- Change city for weather
- All settings are saved locally

## Window Behavior

- **Borderless** transparent window
- **Always on top** of other windows
- **No taskbar icon** - runs as an overlay
- **No window controls** (minimize, maximize, close)
- **Auto-positioned** at top center
- **Smooth animations** for expand/collapse transitions
- **Centered** automatically when expanding/collapsing

## Data Storage

All data is stored locally at:
```
%LocalAppData%\FloatingIsland\
```

Files:
- `settings.json` - User configuration (AI key, city preferences)
- `tasks.json` - Task list

Weather API key for developers:
```
<project-folder>\appsettings.json
```

## Architecture

**Technology Stack:**
- WPF (Windows Presentation Foundation)
- .NET 8.0 (Windows target)
- MVVM Pattern

**Services:**
- `SystemMonitorService` - Battery status, system time
- `WeatherService` - OpenWeather API integration
- `AIService` - OpenRouter API for AI responses
- `MediaService` - Media playback detection (placeholder)
- `ClipboardService` - Clipboard monitoring
- `StorageService` - Local JSON-based persistence

**Models:**
- `SystemInfo`, `MediaInfo`, `TaskItem`, `ClipboardItem`, `AppSettings`

**View Models:**
- `MainViewModel` - Central orchestration with MVVM pattern

## Design Philosophy

The island follows these principles:
- **Continuity** - Smooth, predictable transitions
- **Non-intrusive** - Never steals focus unexpectedly
- **Spatial consistency** - Stays centered during state changes
- **Automatic** - Responds to system events without user setup
- **Minimal** - Shows only essential information
- **Reactive** - Auto-collapses on inactivity or focus loss

## Keyboard Shortcuts

- **Enter** in AI input → Send message
- **Enter** in Task input → Add task
- **Swipe gestures** → Navigate between pages

## Troubleshooting

**Island doesn't appear:**
- Check if the app is running (look in Task Manager)
- Ensure .NET 8.0 is installed
- Try running as administrator

**Weather not showing:**
- Verify OpenWeather API key in appsettings.json
- Check internet connection
- Ensure API key is valid (test at openweathermap.org)

**AI not responding:**
- Configure OpenRouter API key in Settings (Page 6)
- Check internet connection
- Verify API key is valid

**Tasks not saving:**
- Check if %LocalAppData%\FloatingIsland\ folder exists
- Ensure write permissions

## Future Enhancements

- Full Windows Media Control integration for playback controls
- Customizable themes and colors
- Adjustable island dimensions
- Global hotkeys for quick access
- Multiple monitor support with position memory
- Custom animation speeds
- Spotify, YouTube, and other media player integrations
- Voice input for AI
- More keyboard shortcuts

## License

MIT License

## Developer Notes

**Building for Release:**
```powershell
dotnet publish -c Release -r win-x64 --self-contained
```

**Dependencies:**
- Newtonsoft.Json (JSON serialization)
- System.Management (battery monitoring)
- System.Windows.Forms (power status API)

**APIs Used:**
- OpenWeather API (weather data)
- OpenRouter API (AI responses)
