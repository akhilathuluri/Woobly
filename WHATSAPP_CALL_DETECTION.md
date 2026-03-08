# WhatsApp Call Detection — Contact Name Resolution

## The Problem

WhatsApp Desktop is built on **WebView2** (Microsoft's Edge-based browser runtime).
All UI content — including the caller's name shown on the incoming call screen — is
rendered inside a browser viewport. Win32 APIs (`GetWindowText`, `EnumWindows`) can
only read the **Win32 window title**, not browser-rendered content.

During a WhatsApp call, the visible Win32 windows are:

| Window Title | Type |
|---|---|
| `WhatsApp.Root` | Windows App SDK host shell |
| `ReunionCaptionControlsWindow` | Windows App SDK title bar chrome |
| `AppWindow Custom Title` | Windows App SDK custom title bar |
| `Non Client Input Sink Window` | WebView2 input compositor |
| `WebView2: WhatsApp` | WebView2 browser container |

None of these contain the contact's name. Every attempt to read a contact name from
Win32 window titles returns empty or an internal chrome string.

---

## Why Previous Approaches Failed

### Attempt 1 — Read from the keyword window title
`WhatsApp.Root` triggered detection via the `CallKeywords` list. But its title
is literally `"WhatsApp.Root"` — no contact name anywhere.

### Attempt 2 — Scan all WhatsApp windows
Iterated every window of the WhatsApp process with `EnumWindows`. All window titles
were internal WebView2/WinUI chrome (see table above).

### Attempt 3 — `EnumChildWindows` on call window
Called `GetWindowText` on every child HWND of the call window. These are Win32 child
windows hosting the WebView2 substrate — none expose caller information.

### Problem with all Win32 approaches
WhatsApp Desktop's call UI is pure HTML/CSS/JS rendered inside WebView2.
The caller's name exists only in the **DOM**, which is invisible to Win32 APIs.

---

## The Solution — UI Automation (UIA)

Windows Accessibility APIs (`System.Windows.Automation`) can traverse the full
**accessibility tree**, which includes the content rendered inside WebView2.
Every visible text element in the web page is exposed as an `AutomationElement`
with a `Name` property.

### How it works

```
WhatsApp.Root (AutomationElement)
  └─ WebView2 container
       └─ [web content accessibility tree]
            ├─ "Minimize"           ← window chrome button
            ├─ "John Doe"           ← contact name ← TARGET
            ├─ "Incoming call"      ← call status text
            ├─ "Mute"               ← call control button
            └─ "End call"           ← call control button
```

### Implementation

**1. Async background scan** (`ResolveContactAsync`)

When call detection fires with no contact name, a `Task.Run` immediately starts a
background scan so the island expands instantly (showing "Unknown") and the name
updates live once found.

```
Call detected (WhatsApp.Root)
  ↓
CallStarted fired → Island expands → Shows "Unknown"
  ↓ (parallel, background thread)
Task.Run → ResolveContactAsync
  → Strategy 1: EnumChildWindows       (fast, ~0 ms, usually empty for WebView2)
  → Strategy 2: GetContactViaUIA       (reads accessibility tree, ~500-1500 ms)
  → ContactNameResolved event fired on UI thread
  → ActiveCall.ContactName updated
  → XAML binding refreshes → Shows "John Doe"
```

**2. UIA tree walk** (`WalkUIA`)

Uses `TreeWalker.ContentViewWalker` which skips invisible layout/chrome elements,
making it significantly faster than `RawViewWalker` on deep WebView2 trees.

Limits: max **15 levels deep**, max **300 nodes** — prevents hanging on
pathological DOM structures.

**3. Three-layer noise filtering**

The UIA tree contains many non-contact text nodes. Three filters prevent false
positives:

| Layer | What it blocks | Example blocked values |
|---|---|---|
| `UiControlNames` HashSet | Button and control accessible names | `"Minimize"`, `"Close"`, `"Mute"`, `"End call"`, `"Back"` |
| `knownInternalTitles` array | Exact Win32/WebView2 chrome window titles | `"Non Client Input Sink Window"`, `"ReunionCaptionControlsWindow"`, `"AppWindow Custom Title"` |
| `techFragments` word list | Candidates whose every word is a tech word | `"non client input sink"`, `"reunion caption controls"` |

**4. Live XAML update**

`CallInfo.ContactName` implements `INotifyPropertyChanged`. When the background
task resolves the name, it fires `ContactNameResolved` on the UI dispatcher thread:

```csharp
_callDetectionService.ContactNameResolved += name =>
{
    if (ActiveCall.IsActive)
        ActiveCall.ContactName = name;  // XAML binding updates automatically
};
```

---

## Final Call Detection Flow (WhatsApp)

```
1. Poll tick (every 1200 ms)
2. Scan process list → find "WhatsApp" PIDs
3. EnumWindows → find window titled "WhatsApp.Root"
4. "whatsapp.root" matches CallKeywords → Signal 1 detected
5. Store _trackedCallHandle = WhatsApp.Root HWND
6. _trackedCallContact = "" (no name from Win32)
7. CallStarted event → island expands → shows "WhatsApp / Unknown"
8. Task.Run → ResolveContactAsync (background)
   a. Sleep 600 ms (let call overlay fully render)
   b. EnumChildWindows → all titles are internal → nothing found
   c. AutomationElement.FromHandle(WhatsApp.Root HWND)
   d. WalkUIA → scan content tree nodes
   e. Hit "John Doe" AutomationElement → ExtractContactName validates → returns "John Doe"
   f. _trackedCallContact = "John Doe"
   g. Dispatcher.BeginInvoke → ContactNameResolved("John Doe")
9. MainViewModel → ActiveCall.ContactName = "John Doe"
10. XAML refreshes → shows "WhatsApp / John Doe"
11. Subsequent polls → IsWindowVisible(_trackedCallHandle) = true → stable detection
12. WhatsApp.Root closes (call ended) → IsWindowVisible = false → CallEnded
```

---

## Files Changed

| File | Change |
|---|---|
| `Services/CallDetectionService.cs` | Core detection logic, UIA scan, noise filters |
| `ViewModels/MainViewModel.cs` | Subscribed to `ContactNameResolved` event |
| `Models/CallInfo.cs` | `ContactName` has `INotifyPropertyChanged` for live update |
