using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Threading;
using Woobly.Models;

namespace Woobly.Services
{
    /// <summary>
    /// Detects active WhatsApp and Telegram calls by scanning visible window titles
    /// of those processes. No special permissions or app packaging is required.
    /// Fires <see cref="CallStarted"/> / <see cref="CallEnded"/> events.
    /// </summary>
    public class CallDetectionService : IDisposable
    {
        // ── Win32 ────────────────────────────────────────────────────────────
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        private const int SW_RESTORE = 9;

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        private const int GWL_EXSTYLE   = -20;
        private const int WS_EX_TOPMOST = 0x00000008;

        // ── App patterns ─────────────────────────────────────────────────────

        // Process names to monitor (lowercase, no .exe extension)
        private static readonly string[] WatchedProcesses = { "whatsapp", "telegram" };

        // Window-title substrings that indicate an active call (lowercase)
        private static readonly string[] CallKeywords =
        {
            "incoming call", "incoming voice", "incoming video",
            "voice call", "video call", "audio call",
            "calling", "ringing",
            "call with",                    // Telegram active-call window
            "active call", "on a call",
            "whatsapp.root"                 // WhatsApp call overlay window
        };

        // Keywords that indicate an outgoing call
        private static readonly string[] OutgoingKeywords = { "calling", "outgoing" };

        // Keywords that indicate an incoming call
        private static readonly string[] IncomingKeywords = { "incoming", "ringing", "whatsapp.root" };

        // ── State ─────────────────────────────────────────────────────────────
        private readonly DispatcherTimer _timer;
        private bool _isRunning;
        private CallInfo? _lastCall;
        // Per-app previously-seen window handles — used to spot brand-new popup windows
        private readonly Dictionary<string, HashSet<IntPtr>> _knownHandles = new();
        // Captured dispatcher so background tasks can marshal events back to the UI thread
        private readonly Dispatcher _dispatcher;

        // Once a call window is identified, persist it by handle so every poll just
        // calls IsWindowVisible() rather than re-running the "is it new?" logic.
        private IntPtr _trackedCallHandle  = IntPtr.Zero;
        private string _trackedCallContact = "";
        private string _trackedCallApp     = "";

        // ── Events ────────────────────────────────────────────────────────────
        public event Action<CallInfo>? CallStarted;
        public event Action? CallEnded;
        /// <summary>
        /// Fires when a contact name is resolved asynchronously (e.g. via UI Automation
        /// for WhatsApp Desktop which doesn't expose names in Win32 window titles).
        /// Always raised on the UI/dispatcher thread.
        /// </summary>
        public event Action<string>? ContactNameResolved;

        public CallDetectionService()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1200)
            };
            _timer.Tick += (_, _) => Poll();
            Start();
        }

        public void Start()
        {
            if (_isRunning) return;
            _timer.Start();
            _isRunning = true;
        }

        public void Stop()
        {
            if (!_isRunning) return;
            _timer.Stop();
            _isRunning = false;
        }

        // ── Public helpers ───────────────────────────────────────────────────

        /// <summary>Brings the call window to the foreground so the user can interact.</summary>
        public void BringCallWindowToFront()
        {
            if (_lastCall?.WindowHandle is nint h && h != 0)
            {
                ShowWindow(h, SW_RESTORE);
                SetForegroundWindow(h);
            }
        }

        // ── Core polling logic ───────────────────────────────────────────────

        private void Poll()
        {
            var detected = TryScanWindows();

            if (detected != null)
            {
                bool isNew = _lastCall == null ||
                             _lastCall.ContactName != detected.ContactName ||
                             _lastCall.AppSource   != detected.AppSource;

                _lastCall = detected;
                if (isNew) CallStarted?.Invoke(detected);
                else
                {
                    // Update direction/handle in case they changed mid-call
                    _lastCall.Direction     = detected.Direction;
                    _lastCall.WindowHandle  = detected.WindowHandle;
                }
            }
            else if (_lastCall != null)
            {
                _lastCall = null;
                CallEnded?.Invoke();
            }
        }

        private CallInfo? TryScanWindows()
        {
            // Build pid → app-label map
            var watchedPids = new Dictionary<uint, string>();
            foreach (var proc in System.Diagnostics.Process.GetProcesses())
            {
                try
                {
                    var name = proc.ProcessName.ToLowerInvariant();
                    foreach (var watched in WatchedProcesses)
                    {
                        if (name.Contains(watched))
                        {
                            watchedPids[(uint)proc.Id] = ToProperName(watched);
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    AppLog.Warn($"Skipping process during call scan: {ex.Message}");
                }
            }
            if (watchedPids.Count == 0)
            {
                _trackedCallHandle = IntPtr.Zero; // watched app exited
                return null;
            }

            // Seed baseline handles for apps currently in background (no visible windows).
            // Ensures prevHandles is non-null on the very next poll so a new popup
            // is detected as "new" even when Telegram was sitting in the system tray.
            foreach (var appLabel in watchedPids.Values.Distinct())
                if (!_knownHandles.ContainsKey(appLabel))
                    _knownHandles[appLabel] = new HashSet<IntPtr>();

            // ── Persistence: if we already know the call window, just verify it ─────────
            // This is the KEY FIX: Signal 2 (new-window) fires ONCE on the first poll.
            // Without persistence, Telegram's "My Number2" window enters _knownHandles →
            // Signal 2 never fires again → TryScanWindows returns null → CallEnded 1.2 s later.
            if (_trackedCallHandle != IntPtr.Zero)
            {
                if (IsWindowVisible(_trackedCallHandle))
                {
                    return new CallInfo
                    {
                        IsActive     = true,
                        ContactName  = _trackedCallContact,
                        AppSource    = _trackedCallApp,
                        Direction    = CallDirection.Incoming,
                        WindowHandle = _trackedCallHandle
                    };
                }
                // Call window closed — clear and fall through to fresh detection
                _trackedCallHandle  = IntPtr.Zero;
                _trackedCallContact = "";
                _trackedCallApp     = "";
            }

            // Collect ALL visible windows per app
            var appWindows = new Dictionary<string, List<(IntPtr hWnd, string title)>>();
            EnumWindows((hWnd, _) =>
            {
                if (!IsWindowVisible(hWnd)) return true;
                GetWindowThreadProcessId(hWnd, out uint pid);
                if (!watchedPids.TryGetValue(pid, out var app)) return true;
                var sb = new StringBuilder(512);
                GetWindowText(hWnd, sb, 512);
                var title = sb.ToString().Trim();
                if (string.IsNullOrEmpty(title)) return true;
                if (!appWindows.TryGetValue(app, out var lst))
                    appWindows[app] = lst = new();
                lst.Add((hWnd, title));
                return true;
            }, IntPtr.Zero);

            foreach (var (appName, windows) in appWindows)
            {
                _knownHandles.TryGetValue(appName, out var prevHandles);
                _knownHandles[appName] = new HashSet<IntPtr>(windows.Select(w => w.hWnd));

                // Signal 1: any window title contains a known call keyword
                // e.g. WhatsApp → "WhatsApp.Root",  generic → "Voice call | Telegram"
                foreach (var (h, t) in windows)
                {
                    if (!CallKeywords.Any(k => t.ToLowerInvariant().Contains(k))) continue;

                    // Try to find a contact name from any window of this app
                    var contact = "";
                    foreach (var (_, t2) in windows)
                    {
                        contact = ExtractContactName(t2, t2.ToLowerInvariant(), appName);
                        if (!string.IsNullOrEmpty(contact)) break;
                    }

                    _trackedCallHandle  = h;
                    _trackedCallContact = contact;
                    _trackedCallApp     = appName;

                    // WhatsApp Desktop never puts contact names in Win32 window titles.
                    // Kick off an async scan via child-window text + UI Automation so we
                    // can update the contact name once the accessibility tree is ready.
                    if (string.IsNullOrEmpty(contact))
                    {
                        var capturedHandle  = h;
                        var capturedWindows = windows.Select(w => w.hWnd).ToList();
                        var capturedApp     = appName;
                        _ = Task.Run(() => ResolveContactAsync(capturedHandle, capturedWindows, capturedApp));
                    }

                    return new CallInfo
                    {
                        IsActive     = true,
                        ContactName  = contact,
                        AppSource    = appName,
                        Direction    = DetectDirection(t.ToLowerInvariant()),
                        WindowHandle = h
                    };
                }

                // Signal 2: brand-new popup window with no call keyword in its title.
                // This is how Telegram's ringing notification is detected:
                //   idle → tray (no windows), call arrives → new window titled "My Number2"
                if (prevHandles == null) continue;
                foreach (var (h, t) in windows)
                {
                    if (prevHandles.Contains(h)) continue;         // not a new window
                    if (!IsLikelyCallPopup(h, t, appName)) continue;

                    // Window title IS the contact name for Telegram
                    _trackedCallHandle  = h;
                    _trackedCallContact = t;
                    _trackedCallApp     = appName;
                    return new CallInfo
                    {
                        IsActive     = true,
                        ContactName  = t,
                        AppSource    = appName,
                        Direction    = CallDirection.Incoming,
                        WindowHandle = h
                    };
                }
            }
            return null;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Called on a background thread when a call is detected but no contact name
        /// was found in Win32 window titles (typical for WhatsApp Desktop / WebView2).
        /// Tries two strategies in order:
        ///   1. EnumChildWindows — instant, works when child hwnd text is accessible.
        ///   2. UI Automation tree walk — reads WebView2 accessibility tree content.
        /// When a name is found, updates _trackedCallContact and fires ContactNameResolved
        /// on the UI dispatcher thread so XAML bindings update automatically.
        /// </summary>
        private void ResolveContactAsync(IntPtr callHandle, List<IntPtr> allHandles, string appName)
        {
            // Small delay so the call overlay has time to fully render
            System.Threading.Thread.Sleep(600);

            // Strategy 1: child-window GetWindowText (fast, works for some apps)
            var name = "";
            foreach (var h in allHandles)
            {
                name = GetContactFromChildWindows(h, appName);
                if (!string.IsNullOrEmpty(name)) break;
            }

            // Strategy 2: UI Automation accessibility tree (handles WebView2 content)
            if (string.IsNullOrEmpty(name))
                name = GetContactViaUIA(callHandle, appName);

            if (string.IsNullOrEmpty(name)) return;

            // Update the persisted contact and notify subscribers on the UI thread
            _trackedCallContact = name;
            _dispatcher.BeginInvoke(() => ContactNameResolved?.Invoke(name));
        }

        /// <summary>Scans child window texts looking for a contact name.</summary>
        private static string GetContactFromChildWindows(IntPtr hWnd, string appName)
        {
            // Known internal window titles to skip immediately
            var internalPrefixes = new[] {
                "non client", "non-client", "corewindow", "webview",
                "reunioncaption", "reunionwindowing", "applicationframe",
                "appwindow", "custom title",
                "microsoft edge", "chromium", "gpu", "storage", "network",
                "service worker", "runtime broker", "crashpad"
            };

            var texts = new List<string>();
            EnumChildWindows(hWnd, (child, _) =>
            {
                var sb = new StringBuilder(256);
                GetWindowText(child, sb, 256);
                var text = sb.ToString().Trim();
                if (text.Length < 2) return true;
                var lower = text.ToLowerInvariant();
                if (internalPrefixes.Any(p => lower.StartsWith(p))) return true; // skip chrome
                texts.Add(text);
                return true;
            }, IntPtr.Zero);

            foreach (var text in texts)
            {
                var contact = ExtractContactName(text, text.ToLowerInvariant(), appName);
                if (!string.IsNullOrEmpty(contact)) return contact;
            }
            return "";
        }

        /// <summary>
        /// Walks the UI Automation content tree up to 300 nodes, returning the first
        /// accessible name that looks like a contact name. Uses ContentViewWalker to
        /// skip non-content (chrome/layout) elements for speed on WebView2 apps.
        /// </summary>
        private static string GetContactViaUIA(IntPtr handle, string appName)
        {
            try
            {
                var root = System.Windows.Automation.AutomationElement.FromHandle(handle);
                if (root == null) return "";
                var counter = new[] { 0 };
                return WalkUIA(root, appName, 0, counter);
            }
            catch (Exception ex)
            {
                AppLog.Warn($"UI Automation contact resolution failed: {ex.Message}");
                return "";
            }
        }

        private static readonly HashSet<string> UiControlNames = new(StringComparer.OrdinalIgnoreCase)
        {
            // Window chrome
            "minimize", "maximize", "restore", "close", "snap",
            // Navigation
            "back", "forward", "home", "refresh", "reload",
            // Common actions
            "search", "settings", "menu", "more", "options",
            "accept", "decline", "cancel", "ok", "yes", "no",
            "mute", "unmute", "video", "audio", "speaker",
            "end call", "end", "hang up",
            // Generic UI labels
            "button", "toolbar", "statusbar", "titlebar", "tab",
            "scroll", "pane", "panel", "sidebar", "header", "footer"
        };

        private static string WalkUIA(
            System.Windows.Automation.AutomationElement el,
            string appName, int depth, int[] counter)
        {
            if (depth > 15 || counter[0] > 300) return "";
            counter[0]++;
            try
            {
                var nodeName = el.Current.Name?.Trim();
                if (!string.IsNullOrEmpty(nodeName) && nodeName.Length is >= 2 and <= 100
                    && !UiControlNames.Contains(nodeName))
                {
                    var contact = ExtractContactName(nodeName, nodeName.ToLowerInvariant(), appName);
                    if (!string.IsNullOrEmpty(contact)) return contact;
                }

                var walker = System.Windows.Automation.TreeWalker.ContentViewWalker;
                var child  = walker.GetFirstChild(el);
                while (child != null)
                {
                    var found = WalkUIA(child, appName, depth + 1, counter);
                    if (!string.IsNullOrEmpty(found)) return found;
                    child = walker.GetNextSibling(child);
                }
            }
            catch (Exception ex)
            {
                AppLog.Warn($"UIA node walk failed: {ex.Message}");
            }
            return "";
        }

        /// <summary>
        /// Returns true when a newly-appeared window looks like a call notification popup
        /// (small size + TOPMOST style) rather than the normal app main window.
        /// Used for apps like Telegram whose call popups don't contain call keywords.
        /// </summary>
        private static bool IsLikelyCallPopup(IntPtr hWnd, string title, string appName)
        {
            if (title.Length < 2) return false;

            // Reject the main app window — its title will equal or contain the app name
            if (title.Equals(appName, StringComparison.OrdinalIgnoreCase)) return false;
            if (title.ToLowerInvariant().Contains(appName.ToLowerInvariant())) return false;

            // Reject windows whose title already fired Signal 1 (handled by keyword path)
            if (CallKeywords.Any(k => title.ToLowerInvariant().Contains(k))) return false;

            // Must have actual pixel dimensions (eliminates zero-size ghost handles)
            GetWindowRect(hWnd, out RECT r);
            return r.Right - r.Left > 20 && r.Bottom - r.Top > 20;
        }
        private static CallInfo BuildCallInfo(
            string windowTitle, string lowerTitle, string appSource, IntPtr handle)
        {
            var direction = DetectDirection(lowerTitle);
            var contact   = ExtractContactName(windowTitle, lowerTitle, appSource);

            return new CallInfo
            {
                IsActive      = true,
                ContactName   = contact,
                AppSource     = appSource,
                Direction     = direction,
                WindowHandle  = handle
            };
        }

        private static CallDirection DetectDirection(string lower)
        {
            foreach (var kw in IncomingKeywords)
                if (lower.Contains(kw)) return CallDirection.Incoming;

            foreach (var kw in OutgoingKeywords)
                if (lower.Contains(kw)) return CallDirection.Outgoing;

            return CallDirection.Active;
        }

        private static string ExtractContactName(string title, string lower, string appSource)
        {
            // Reject known UI control / button names — these come from the UIA tree
            // and are never contact names (e.g. "Minimize", "Close", "Mute").
            if (UiControlNames.Contains(title.Trim()))
                return string.Empty;

            // Titles that are 100% known Windows/WebView2 internal chrome — never a contact.
            string[] knownInternalTitles = {
                "non client input sink window", "non-client input sink",
                "corewindow", "applicationframewindow",
                "reunioncaptioncontrolswindow", "reunionwindowingcaptioncontrols",
                "appwindow custom title", "appwindow", "custom title bar",
                "microsoft edge", "chromium", "webview2",
                "storage partition", "storage notification",
                "network notification", "network partition",
                "service worker", "gpu process", "gpu",
                "runtime broker", "crashpad", "handler",
                "utility", "browser"
            };
            if (knownInternalTitles.Any(bad =>
                lower.Equals(bad, StringComparison.OrdinalIgnoreCase) ||
                lower.StartsWith(bad, StringComparison.OrdinalIgnoreCase)))
                return string.Empty;

            // Split on common window-title separators so each segment is evaluated independently.
            // e.g. "Voice call | WhatsApp" → ["Voice call ", " WhatsApp"]
            // This prevents the app name leaking into the contact candidate.
            var separators = new[] { " | ", " \u2013 ", " \u2014 ", " \u00b7 ", " - " };
            var parts = new System.Collections.Generic.List<string> { lower };
            foreach (var sep in separators)
            {
                var next = new System.Collections.Generic.List<string>();
                foreach (var p in parts)
                    next.AddRange(p.Split(new[] { sep }, StringSplitOptions.None));
                parts = next;
            }

            string[] noise = {
                "incoming voice call", "incoming video call", "incoming call",
                "incoming voice", "incoming video", "voice call", "video call",
                "audio call", "call with", "calling...", "calling", "ringing",
                "active call", "on a call",
                "whatsapp.root",            // WhatsApp call overlay window chrome
                appSource.ToLowerInvariant()
            };

            // Single-word technical fragments that appear in window titles but are never
            // contact names (e.g. "WhatsApp.Root" → strips to "root" → reject).
            string[] techFragments = {
                "root", "app", "main", "host", "gpu", "manager",
                "worker", "webview", "utility", "service", "broker", "crashpad",
                "sink", "client", "input", "window", "handler", "process",
                "partition", "notification", "storage", "network", "browser",
                "reunion", "caption", "controls", "frame", "hosting",
                "custom", "title", "bar", "titlebar"
            };

            foreach (var part in parts)
            {
                var candidate = part.Trim();
                foreach (var n in noise)
                    candidate = candidate.Replace(n, " ");

                candidate = candidate.Trim(' ', '-', '|', '\u2013', '\u2014', '\u00b7', '\u2022', '.', '(', ')', '[', ']');
                candidate = System.Text.RegularExpressions.Regex.Replace(candidate, @"\s{2,}", " ").Trim();

                // Reject if empty, equals app name, is a single tech fragment, or
                // contains only tech-fragment words (e.g. "non client input sink")
                if (candidate.Length < 2 ||
                    candidate.Equals(appSource.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase) ||
                    techFragments.Any(f => candidate.Equals(f, StringComparison.OrdinalIgnoreCase)) ||
                    candidate.Split(' ').All(w => techFragments.Contains(w.ToLowerInvariant())))
                    continue;

                // Re-case from the original title for proper capitalisation
                var idx = lower.IndexOf(candidate, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0 && idx + candidate.Length <= title.Length)
                    return title.Substring(idx, candidate.Length).Trim();

                return Capitalise(candidate);
            }

            return string.Empty; // no identifiable contact name — UI shows "Unknown"
        }

        private static string Capitalise(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return char.ToUpperInvariant(s[0]) + s[1..];
        }

        private static string ToProperName(string processKeyword) => processKeyword switch
        {
            "whatsapp" => "WhatsApp",
            "telegram" => "Telegram",
            _ => Capitalise(processKeyword)
        };

        public void Dispose()
        {
            Stop();
        }
    }
}
