# Woobly - Intelligent Desktop Companion

A Windows desktop companion inspired by Dynamic Island, providing a persistent floating interface with multiple interactive features.

## Features

- **Collapsed State**: Displays time, weather, and battery status
- **Expanded State**: Access 6 interactive pages by swiping
  - Page 1: System Overview (detailed time, date, weather, battery)
  - Page 2: Media Control (auto-detects playing media)
  - Page 3: AI Assistant (provider selectable: OpenRouter or Groq)
  - Page 4: Task Memory (quick task management)
  - Page 5: Clipboard Memory (history size configurable)
  - Page 6: Settings
- **Privacy Controls**: Clipboard and call monitoring are consent-gated and disabled by default
- **Secure AI Keys**: AI API keys are stored with Windows DPAPI (per-user encryption)
- **Structured Logging**: Runtime diagnostics written to local log file

## System Requirements

- Windows 10/11
- .NET 8.0 Runtime or SDK

## Setup

### 1. Clone/Download the Project
```powershell
cd e:\Woobly
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
.\bin\Debug\net8.0-windows10.0.19041.0\Woobly.exe
```

## Configuration

### Weather Settings
The OpenWeather API key is configured by the developer in [appsettings.json](appsettings.json). Users can change the city in Settings (Page 6).

### AI Settings (User Configuration)
Users must configure their own AI settings:
1. Click the island to expand
2. Swipe to Page 6 (Settings)
3. Select provider: OpenRouter or Groq
4. Enter your provider API key
5. Optionally change the model
6. Click "Save Settings"

### Privacy Consent (Required for Monitoring Features)
Monitoring is off by default. To enable:
1. Open Page 6 (Settings)
2. Check consent: "I understand and consent to local monitoring features"
3. Enable specific features:
  - Clipboard monitoring
  - WhatsApp/Telegram call monitoring
4. Save settings

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
- Captures copied text only when monitoring is enabled in Settings
- Click any item to restore it to clipboard
- Shows preview (first 100 characters)
- History length is configurable in Settings

**Page 6 - Settings**
- Configure AI provider, model, and API key
- Configure privacy consent and monitoring toggles
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
%LocalAppData%\Woobly\
```

Files:
- `settings.json` - User configuration (non-sensitive settings)
- `tasks.json` - Task list
- `woobly.log` - Structured runtime logs

Secure secrets:
- AI API keys are stored encrypted via Windows DPAPI under the same LocalAppData area

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
- `AIService` - Provider-routed AI streaming (OpenRouter/Groq)
- `MediaService` - Media playback detection (placeholder)
- `ClipboardService` - Clipboard monitoring
- `StorageService` - Local JSON-based persistence
- `SecretStore` - DPAPI-backed secret storage
- `AppLogger` - Structured logging

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
- Configure provider + API key in Settings (Page 6)
- Check internet connection
- Verify provider key and model are valid

**Tasks not saving:**
- Check if %LocalAppData%\Woobly\ folder exists
- Ensure write permissions

**Clipboard or call features not active:**
- Ensure privacy consent is checked in Settings
- Ensure the specific monitoring toggle is enabled

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
- Groq API (AI responses)

**Run Tests:**
```powershell
dotnet test Woobly.Tests/Woobly.Tests.csproj
```
