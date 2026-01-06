using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace PersonalFlyout
{
    /// <summary>
    /// Suppresses the Windows native media overlay by moving it off-screen.
    /// Uses efficient polling and window position manipulation to avoid conflicts.
    /// </summary>
    public class MediaOverlaySuppressor : IDisposable
    {
        // Window positioning constants
        private const int OFF_SCREEN_X = -3000;
        private const int OFF_SCREEN_Y = -3000;
        
        // SetWindowPos flags
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_ASYNCWINDOWPOS = 0x4000;

        // Hook constants
        private const int WH_SHELL = 10;
        private const int HSHELL_WINDOWCREATED = 1;
        private const int HSHELL_APPCOMMAND = 12;

        // AppCommands
        private const int APPCOMMAND_MEDIA_NEXTTRACK = 11;
        private const int APPCOMMAND_MEDIA_PREVIOUSTRACK = 12;
        private const int APPCOMMAND_MEDIA_STOP = 13;
        private const int APPCOMMAND_MEDIA_PLAY_PAUSE = 14;

        public event Action<string>? MediaCommandReceived;

        private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        
        private HookProc _shellHookDelegate;
        private IntPtr _hookHandle;
        private System.Threading.Timer? _enumTimer;

        // P/Invoke declarations
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        /// <summary>
        /// Starts the suppressor with event-driven hooks and efficient polling.
        /// </summary>
        public void Start()
        {
            if (_hookHandle != IntPtr.Zero)
                return; // Already started

            _shellHookDelegate = ShellHookCallback;
            _hookHandle = SetWindowsHookEx(WH_SHELL, _shellHookDelegate, IntPtr.Zero, GetCurrentThreadId());

            if (_hookHandle == IntPtr.Zero)
            {
                System.Diagnostics.Debug.WriteLine("Failed to set Windows hook for media overlay suppression");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Media overlay suppressor started successfully");
            }

            // Start optimized polling - 150ms interval for HPC efficiency
            _enumTimer = new System.Threading.Timer(EnumerateAndSuppressOverlays, null, 150, 150);
        }

        /// <summary>
        /// Stops the suppressor and releases resources.
        /// </summary>
        public void Stop()
        {
            _enumTimer?.Dispose();
            _enumTimer = null;

            if (_hookHandle != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookHandle);
                _hookHandle = IntPtr.Zero;
                System.Diagnostics.Debug.WriteLine("Media overlay suppressor stopped");
            }
        }

        private void EnumerateAndSuppressOverlays(object? state)
        {
            try
            {
                EnumWindows((hWnd, lParam) =>
                {
                    if (!IsWindowVisible(hWnd))
                        return true;

                    StringBuilder className = new StringBuilder(256);
                    GetClassName(hWnd, className, className.Capacity);
                    string name = className.ToString();

                    // Get window title
                    int length = GetWindowTextLength(hWnd);
                    StringBuilder title = new StringBuilder(length + 1);
                    GetWindowText(hWnd, title, title.Capacity);
                    string windowTitle = title.ToString();

                    // Get window position for logging
                    GetWindowRect(hWnd, out RECT rect);
                    GetWindowThreadProcessId(hWnd, out uint processId);

                    // LOG EVERYTHING to find the media overlay
                    // This will help us identify the exact window class
                    if (!string.IsNullOrEmpty(name) || !string.IsNullOrEmpty(windowTitle))
                    {
                        System.Diagnostics.Debug.WriteLine($"[SCAN] Class: '{name}' | Title: '{windowTitle}' | Pos: ({rect.Left},{rect.Top}) | PID: {processId}");
                    }

                    // Check if this is a media overlay window
                    if (ShouldSuppressWindow(name, windowTitle))
                    {
                        SuppressWindow(hWnd, name, windowTitle);
                    }
                    // Also check for NativeHWNDHost and enumerate its children
                    else if (name == "NativeHWNDHost" || name.Contains("Windows.UI.Core.CoreWindow"))
                    {
                        CheckAndSuppressChildren(hWnd, name);
                    }

                    return true;
                }, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in window enumeration: {ex.Message}");
            }
        }

        private void CheckAndSuppressChildren(IntPtr parentHwnd, string parentClassName)
        {
            try
            {
                EnumChildWindows(parentHwnd, (childHwnd, lParam) =>
                {
                    StringBuilder childClassName = new StringBuilder(256);
                    GetClassName(childHwnd, childClassName, childClassName.Capacity);
                    string childName = childClassName.ToString();

                    // Check for DirectUIHWND or other media-related child windows
                    if (childName == "DirectUIHWND" || childName.Contains("MediaTransport") || 
                        childName.Contains("MediaControl"))
                    {
                        int length = GetWindowTextLength(childHwnd);
                        StringBuilder title = new StringBuilder(length + 1);
                        GetWindowText(childHwnd, title, title.Capacity);
                        string childTitle = title.ToString();

                        if (ShouldSuppressWindow(childName, childTitle))
                        {
                            SuppressWindow(childHwnd, $"{parentClassName}>{childName}", childTitle);
                        }
                    }

                    return true;
                }, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error enumerating child windows: {ex.Message}");
            }
        }

        private void SuppressWindow(IntPtr hWnd, string className, string title)
        {
            try
            {
                // Get current window position
                if (GetWindowRect(hWnd, out RECT rect))
                {
                    int width = rect.Right - rect.Left;
                    int height = rect.Bottom - rect.Top;

                    // Get process name to identify Spotify
                    GetWindowThreadProcessId(hWnd, out uint processId);
                    string processName = GetProcessName(processId);

                    // Filter by size: Spotify's overlay is small (~300x100)
                    // Main windows are much larger (>500px wide)
                    bool isSmallOverlay = width < 500 && height < 200 && width > 50 && height > 50;

                    // Spotify-specific: Suppress Chrome_WidgetWin_1 windows that show song info
                    bool isSpotifyOverlay = processName.Equals("Spotify", StringComparison.OrdinalIgnoreCase) &&
                                           className.Contains("Chrome_WidgetWin") &&
                                           !string.IsNullOrEmpty(title) &&
                                           title != "Spotify" && // Don't suppress main window
                                           title != "Spotify Premium" && // Don't suppress main window
                                           title != "Spotify Free"; // Don't suppress main window

                    // Only suppress if it's a small overlay OR already confirmed media control OR Spotify overlay
                    bool shouldSuppress = isSmallOverlay || 
                                         isSpotifyOverlay ||
                                         className.Contains("MediaTransport") ||
                                         className.Contains("MediaOverlay") ||
                                         className.Contains("VolumeOverlay");

                    if (!shouldSuppress)
                    {
                        // Don't suppress large windows
                        return;
                    }

                    // Only move if not already off-screen (optimization to reduce API calls)
                    if (rect.Left > -2000 && rect.Top > -2000)
                    {
                        // Move window off-screen instead of hiding it
                        bool success = SetWindowPos(
                            hWnd,
                            IntPtr.Zero,
                            OFF_SCREEN_X,
                            OFF_SCREEN_Y,
                            0,
                            0,
                            SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_ASYNCWINDOWPOS
                        );

                        if (success)
                        {
                            System.Diagnostics.Debug.WriteLine($">>> SUPPRESSED (moved off-screen): {className} | '{title}' | Process: {processName} | Size: {width}x{height} | Pos: ({rect.Left},{rect.Top})");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error suppressing window: {ex.Message}");
            }
        }

        private string GetProcessName(uint processId)
        {
            try
            {
                using (Process process = Process.GetProcessById((int)processId))
                {
                    return process.ProcessName;
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        private bool ShouldSuppressWindow(string className, string title)
        {
            // Target specific media overlay windows
            // Be careful not to suppress main application windows
            
            // Exclude main app windows
            if (className.Contains("ApplicationFrameWindow"))
                return false;

            // Target known media overlay class names
            if (className.Contains("MediaTransportControls") ||
               className == "NativeHWNDHost" ||
               className.Contains("MediaOverlay") ||
               className.Contains("VolumeOverlay") ||
               className.Contains("VolumeControl") ||
               title.Contains("Media Transport Controls") ||
               title.Contains("Volume Control"))
            {
                return true;
            }

            // Target Spotify/Chrome overlay windows - we'll filter by size
            if (className.Contains("Chrome_WidgetWin"))
            {
                return true; // We'll check size in SuppressWindow
            }

            return false;
        }

        private IntPtr ShellHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode == HSHELL_WINDOWCREATED)
            {
                try
                {
                    StringBuilder className = new StringBuilder(256);
                    GetClassName(wParam, className, className.Capacity);
                    string name = className.ToString();

                    int length = GetWindowTextLength(wParam);
                    StringBuilder title = new StringBuilder(length + 1);
                    GetWindowText(wParam, title, title.Capacity);
                    string windowTitle = title.ToString();

                    // Log window creations for debugging
                    if (name.Contains("Media") || name.Contains("Volume") || name.Contains("Transport"))
                    {
                        System.Diagnostics.Debug.WriteLine($"Window created: {name} | '{windowTitle}'");
                    }

                    if (ShouldSuppressWindow(name, windowTitle))
                    {
                        SuppressWindow(wParam, name, windowTitle);
                    }
                    else if (name == "NativeHWNDHost" || name.Contains("Windows.UI.Core.CoreWindow"))
                    {
                        // Check children immediately on creation
                        CheckAndSuppressChildren(wParam, name);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in shell hook: {ex.Message}");
                }
            }
            else if (nCode == HSHELL_APPCOMMAND)
            {
                try
                {
                    int cmd = ((int)lParam >> 16) & ~0xF000;
                    
                    if (cmd == APPCOMMAND_MEDIA_NEXTTRACK)
                    {
                        MediaCommandReceived?.Invoke("Next");
                    }
                    else if (cmd == APPCOMMAND_MEDIA_PREVIOUSTRACK)
                    {
                        MediaCommandReceived?.Invoke("Prev");
                    }
                }
                catch { }
            }

            return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
