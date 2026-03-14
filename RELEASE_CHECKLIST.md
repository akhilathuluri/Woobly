# Woobly Release Checklist

## 1. Build and Test
- [ ] `dotnet build` succeeds
- [ ] `dotnet test Woobly.Tests/Woobly.Tests.csproj` succeeds
- [ ] No new errors in VS Code Problems panel

## 2. Privacy and Security
- [ ] AI API keys are not present in `%LocalAppData%\\Woobly\\settings.json`
- [ ] Privacy consent defaults are off on fresh install/profile
- [ ] Clipboard monitoring only runs when consent + toggle are enabled
- [ ] Call monitoring only runs when consent + toggle are enabled
- [ ] `%LocalAppData%\\Woobly\\woobly.log` records recoverable runtime issues

## 3. Runtime Smoke Tests
- [ ] App launches and starts collapsed
- [ ] Expand/collapse transitions work
- [ ] Page navigation (swipe, wheel, dots) works
- [ ] AI chat works with OpenRouter
- [ ] AI chat works with Groq
- [ ] Task add/toggle/remove persists across restart
- [ ] Clipboard history behavior matches consent/toggle state
- [ ] Settings save and re-load correctly

## 4. Packaging and Distribution
- [ ] `dotnet publish -c Release -r win-x64 --self-contained` succeeds
- [ ] Published executable exists at `bin/Release/net8.0-windows10.0.19041.0/win-x64/publish/Woobly.exe`
- [ ] Include `README.md`, `LICENSE`, and release notes in distribution artifacts

## 5. Documentation and Legal
- [ ] README and QUICKSTART match current app behavior
- [ ] PROJECT_SUMMARY includes current provider/security model
- [ ] `LICENSE` is present and matches intended license
- [ ] Changelog/release notes mention privacy and AI provider updates
