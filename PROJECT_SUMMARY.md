# Floating Island - Project Summary

## Release Update (March 2026)

The codebase now includes release-hardening improvements while preserving core UX and architecture:

- AI provider routing now supports both OpenRouter and Groq.
- AI API keys are stored securely using Windows DPAPI (not plain JSON).
- Clipboard and call monitoring are consent-gated and disabled by default.
- Structured logging has been added for operational diagnostics.
- Polling services now expose proper start/stop/dispose semantics.
- A basic automated test suite has been added.

### New/Updated Components

- `Services/SecretStore.cs` - DPAPI-backed secret storage abstraction
- `Services/AppLogger.cs` - structured file logger (`%LocalAppData%\\Woobly\\woobly.log`)
- `Services/StorageService.cs` - secure key persistence + settings/task diagnostics
- `Services/ClipboardService.cs` - start/stop/dispose lifecycle + adapter abstraction
- `Services/CallDetectionService.cs` - start/stop/dispose lifecycle
- `Services/MediaService.cs` - disposal and warning diagnostics
- `Models/AppSettings.cs` - privacy consent and monitoring toggles
- `Woobly.Tests/*` - xUnit tests for settings persistence, provider routing, and clipboard behavior

### Updated Runtime Behavior

- Monitoring features require explicit consent in Settings before activation.
- Clipboard monitoring and call monitoring can be toggled independently.
- Existing OpenRouter behavior remains available; Groq is an additional provider option.

### Test Command

```powershell
dotnet test Woobly.Tests/Woobly.Tests.csproj
```

## What Was Built

A Windows desktop companion application inspired by Apple's Dynamic Island, implemented as a WPF application using .NET 8.0. The application creates a persistent floating interface that lives at the top center of the screen, providing quick access to system information, tasks, AI assistance, and more.

## Technical Implementation

### Architecture
- **Framework**: WPF (Windows Presentation Foundation) with .NET 8.0
- **Pattern**: MVVM (Model-View-ViewModel)
- **Language**: C# with nullable reference types enabled
- **Data Storage**: JSON-based local storage

### Project Structure
```
FloatingIsland/
├── Models/                    # Data models
│   ├── SystemInfo.cs         # Time, weather, battery
│   ├── MediaInfo.cs          # Media playback info
│   ├── TaskItem.cs           # Task items
│   ├── ClipboardItem.cs      # Clipboard entries
│   └── AppSettings.cs        # User preferences
├── Services/                  # Business logic
│   ├── SystemMonitorService.cs   # Battery & system info
│   ├── WeatherService.cs         # OpenWeather API
│   ├── AIService.cs              # OpenRouter API
│   ├── MediaService.cs           # Media detection
│   ├── ClipboardService.cs       # Clipboard monitoring
│   └── StorageService.cs         # Local persistence
├── ViewModels/
│   └── MainViewModel.cs      # MVVM view model
├── Converters/
│   └── ValueConverters.cs    # XAML value converters
├── MainWindow.xaml           # Main UI
├── MainWindow.xaml.cs        # Window logic
├── App.xaml                  # Application resources
├── appsettings.json          # Developer config
├── README.md                 # Full documentation
└── QUICKSTART.md             # Quick start guide
```

## Key Features Implemented

### 1. Floating Island Window
- Borderless, transparent window
- Always on top (Win32 API integration)
- No taskbar presence (ShowInTaskbar=False)
- No window controls (minimize, maximize, close)
- Auto-centered positioning at top of screen
- Smooth expand/collapse animations using Storyboards

### 2. Two States
**Collapsed State (150x40)**
- Minimal footprint
- Displays: Time | Temperature | Battery%
- Click to expand
- Subtle, non-intrusive

**Expanded State (400x200)**
- Full interaction surface
- Six swipeable pages
- Navigation dots indicator
- Auto-collapse on: focus loss, 3-second idle, deactivation

### 3. Page System (6 Pages)
**Page 1 - System Overview**
- Large clock (auto-updating)
- Current date
- Weather temperature
- Battery percentage with charging status

**Page 2 - Media Context**
- Auto-detection placeholder (extensible)
- Title, artist, album display
- Progress bar
- Time display

**Page 3 - AI Response**
- OpenRouter API integration
- Text input with Enter-to-send
- Clean response display (no user message echo)
- Configurable model selection

**Page 4 - Task Memory**
- Quick task creation (Enter to add)
- Checkbox for completion
- Remove button
- Persistent storage

**Page 5 - Clipboard Memory**
- Automatic clipboard monitoring (500ms polling)
- Last 2 items stored
- Click to restore to clipboard
- Preview truncation (100 chars)

**Page 6 - Settings**
- AI configuration (model, API key)
- Weather city selection
- Real-time save

### 4. Services Layer

**SystemMonitorService**
- Battery percentage and charging status
- System time (1-second updates)
- Uses System.Windows.Forms.SystemInformation

**WeatherService**
- OpenWeather API integration
- Temperature in Celsius
- Weather condition and icon
- 5-minute auto-refresh

**AIService**
- OpenRouter API calls
- Model selection support
- Error handling
- Async/await pattern

**ClipboardService**
- Real-time clipboard monitoring
- Circular buffer (2 items)
- Thread-safe operations
- Event-driven updates

**StorageService**
- JSON serialization with Newtonsoft.Json
- Settings: %LocalAppData%\FloatingIsland\settings.json
- Tasks: %LocalAppData%\FloatingIsland\tasks.json
- Developer config: appsettings.json (OpenWeather key)

**MediaService**
- Placeholder for Windows Media Control integration
- Event-driven architecture
- Ready for UWP API integration

### 5. Interaction Model

**Mouse Interactions**
- Click collapsed island → Expand
- Swipe left/right → Navigate pages (50px threshold)
- Click outside → Collapse (via OnDeactivated)
- Drag gesture detection with delta calculation

**Keyboard Interactions**
- Enter in AI input → Send message
- Enter in Task input → Add task
- All inputs clear after submission

**Automatic Behaviors**
- 3-second idle timer → Auto-collapse
- Focus loss → Auto-collapse
- Clipboard changes → Auto-capture
- Time updates → Every second
- Weather updates → Every 5 minutes

### 6. Animations & Transitions
- Storyboard-based animations
- CubicEase easing function
- 0.3-second duration
- Simultaneous width/height changes
- Centered position maintenance during transitions
- Smooth visibility toggles

### 7. Data Persistence
**Local Storage Location**
```
%LocalAppData%\FloatingIsland\
├── settings.json    # User preferences
└── tasks.json       # Task list
```

**Developer Configuration**
```
<project-root>\appsettings.json
└── OpenWeatherApiKey  # API key
```

## API Integrations

### OpenWeather API
- **Endpoint**: https://api.openweathermap.org/data/2.5/weather
- **Authentication**: API key in query string
- **Units**: Metric (Celsius)
- **Data Retrieved**: Temperature, condition, icon
- **Refresh**: Every 5 minutes

### OpenRouter API
- **Endpoint**: https://openrouter.ai/api/v1/chat/completions
- **Authentication**: Bearer token
- **Default Model**: anthropic/claude-3.5-sonnet
- **User Configurable**: Model selection in settings

## Design Principles Followed

1. **Non-Intrusive**: Never steals focus unexpectedly
2. **Spatial Continuity**: Stays centered during state changes
3. **Automatic Collapse**: Returns to passive state when not needed
4. **Minimal UI**: Only essential information displayed
5. **Smooth Transitions**: Fluid animations with easing
6. **Event-Driven**: Responds to system events, not polling (where possible)
7. **Local-First**: All data stored locally
8. **Privacy**: No telemetry or external tracking

## Technologies & Libraries

- **.NET 8.0** - Target framework
- **WPF** - UI framework
- **Newtonsoft.Json 13.0.4** - JSON serialization
- **System.Management 10.0.3** - System information
- **System.Windows.Forms** - Battery status API
- **Win32 API** (SetWindowPos) - Topmost window control

## Configuration System

### Developer Configuration (appsettings.json)
```json
{
  "OpenWeatherApiKey": "developer_provided_key"
}
```

### User Configuration (settings.json)
```json
{
  "OpenRouterApiKey": "user_provided_key",
  "OpenRouterModel": "anthropic/claude-3.5-sonnet",
  "OpenWeatherApiKey": "optional_override",
  "City": "London",
  "IslandWidth": 150,
  "IslandHeight": 40,
  "ExpandedWidth": 400,
  "ExpandedHeight": 200,
  "AccentColor": "#FF1E1E1E",
  "AnimationDuration": 0.3,
  "IdleTimeout": 3.0,
  "IgnorePointerWhenInactive": false
}
```

## Build & Deployment

### Development Build
```powershell
dotnet build
```

### Release Build
```powershell
dotnet build -c Release
```

### Standalone Executable
```powershell
dotnet publish -c Release -r win-x64 --self-contained
```

Output: `bin\Release\net8.0-windows\win-x64\publish\FloatingIsland.exe`

## Known Limitations

1. **Media Detection**: Placeholder implementation (requires Windows.Media.Control UWP API)
2. **Single Monitor**: Auto-positioning assumes primary monitor
3. **No Hotkeys**: No global hotkey support (requires low-level keyboard hooks)
4. **No Auto-Start**: Doesn't register for Windows startup
5. **Fixed Animation Speed**: Not yet configurable in UI (though stored in settings)

## Future Enhancement Opportunities

1. **Full Media Integration**: Windows Media Control API for playback controls
2. **Multi-Monitor Support**: Remember position per monitor
3. **Custom Themes**: User-selectable color schemes
4. **Hotkey Registration**: Global hotkeys for expand/collapse
5. **Auto-Start**: Windows startup integration
6. **More Clipboard Types**: Images, files, rich text
7. **Task Categories**: Organize tasks with tags
8. **Weather Forecasts**: Multi-day weather view
9. **Notification Integration**: Windows notification center
10. **Voice Input**: Speech-to-text for AI

## Testing Recommendations

1. Test on multiple screen resolutions
2. Verify battery status with laptop unplugged
3. Test clipboard monitoring with various content types
4. Validate weather API with invalid keys
5. Test AI with rate-limited API keys
6. Verify persistence after app restart
7. Test focus loss scenarios
8. Validate swipe gesture thresholds

## Performance Considerations

- Clipboard monitoring: 500ms polling interval (adjustable)
- Weather updates: 5-minute interval (API quota friendly)
- Time updates: 1-second interval (minimal CPU)
- Idle timer: 3-second timeout (user configurable)
- Animation duration: 0.3 seconds (smooth but not slow)

## Compliance & Best Practices

✅ MVVM pattern for testability
✅ Async/await for I/O operations
✅ Nullable reference types enabled
✅ Proper error handling (try-catch with fallbacks)
✅ Local data storage (privacy-friendly)
✅ API keys user-configurable
✅ Resource cleanup (timer disposal)
✅ Event-driven architecture
✅ Separation of concerns (services layer)

## Summary

A fully functional Windows desktop companion has been successfully built with all the core requirements:
- ✅ Pure Windows (WPF/C#)
- ✅ Local data storage
- ✅ OpenRouter AI integration (user-configured)
- ✅ OpenWeather API (developer-configured)
- ✅ No taskbar presence
- ✅ No window controls
- ✅ Floating, always-on-top
- ✅ Collapsed and expanded states
- ✅ Six interactive pages
- ✅ Smooth animations
- ✅ System monitoring
- ✅ Task management
- ✅ Clipboard history

The application is production-ready with comprehensive documentation and follows Windows desktop application best practices.
