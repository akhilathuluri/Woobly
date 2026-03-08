using System;
using System.Collections.Generic;
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
    public class CallDetectionService
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
            "active call", "on a call"
        };

        // Keywords that indicate an outgoing call
        private static readonly string[] OutgoingKeywords = { "calling", "outgoing" };

        // Keywords that indicate an incoming call
        private static readonly string[] IncomingKeywords = { "incoming", "ringing" };

        // ── State ─────────────────────────────────────────────────────────────
        private readonly DispatcherTimer _timer;
        private CallInfo? _lastCall;

        // ── Events ────────────────────────────────────────────────────────────
        public event Action<CallInfo>? CallStarted;
        public event Action? CallEnded;

        public CallDetectionService()
        {
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1200)
            };
            _timer.Tick += (_, _) => Poll();
            _timer.Start();
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
            // Collect PIDs for watched processes
            var watchedPids = new HashSet<uint>();
            var processLookup = new Dictionary<uint, string>(); // pid → app label

            foreach (var proc in System.Diagnostics.Process.GetProcesses())
            {
                try
                {
                    var name = proc.ProcessName.ToLowerInvariant();
                    foreach (var watched in WatchedProcesses)
                    {
                        if (name.Contains(watched))
                        {
                            watchedPids.Add((uint)proc.Id);
                            processLookup[(uint)proc.Id] = ToProperName(watched);
                            break;
                        }
                    }
                }
                catch { /* process may have exited */ }
            }

            if (watchedPids.Count == 0) return null;

            // Enumerate all visible windows looking for call titles
            CallInfo? found = null;

            EnumWindows((hWnd, _) =>
            {
                if (!IsWindowVisible(hWnd)) return true;

                GetWindowThreadProcessId(hWnd, out uint pid);
                if (!watchedPids.Contains(pid)) return true;

                var sb = new StringBuilder(512);
                GetWindowText(hWnd, sb, sb.Capacity);
                var title = sb.ToString().Trim();
                if (string.IsNullOrEmpty(title)) return true;

                var lower = title.ToLowerInvariant();
                foreach (var kw in CallKeywords)
                {
                    if (lower.Contains(kw))
                    {
                        found = BuildCallInfo(
                            windowTitle: title,
                            lowerTitle: lower,
                            appSource: processLookup[pid],
                            handle: hWnd);
                        return false; // stop enumeration
                    }
                }

                return true; // continue
            }, IntPtr.Zero);

            return found;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

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
                "active call", "on a call", appSource.ToLowerInvariant()
            };

            foreach (var part in parts)
            {
                var candidate = part.Trim();
                foreach (var n in noise)
                    candidate = candidate.Replace(n, " ");

                candidate = candidate.Trim(' ', '-', '|', '\u2013', '\u2014', '\u00b7', '\u2022', '.', '(', ')', '[', ']');
                candidate = System.Text.RegularExpressions.Regex.Replace(candidate, @"\s{2,}", " ").Trim();

                // Must be at least 2 chars and not just the app name again
                if (candidate.Length < 2 ||
                    candidate.Equals(appSource.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase))
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
    }
}
