using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace GlassBar.Dashboard
{
    public partial class App : Application
    {
        private Window _window;
        private Mutex _mutex;

        // Win32 API for bringing existing window to foreground
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

        private const int SW_SHOW    = 5;
        private const int SW_RESTORE = 9;

        public App()
        {
            InitializeComponent();
            this.UnhandledException += (sender, e) =>
            {
                // Write to GlassBar.log so the exception is visible even in production.
                // Do NOT set e.Handled = true — let WER generate a crash dump for diagnosis.
                try
                {
                    var logDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "GlassBar");
                    Directory.CreateDirectory(logDir);
                    File.AppendAllText(
                        Path.Combine(logDir, "GlassBar.log"),
                        $"\n[{DateTime.Now:yyyy-MM-dd HH:mm:ss}][-----][ERROR] " +
                        $"UNHANDLED UI EXCEPTION: {e.Exception}\n");
                }
                catch { /* cannot log — crash proceeds normally */ }

                Debug.WriteLine($"[UNHANDLED] {e.Exception}");
                // e.Handled intentionally left false so WER captures a minidump.
            };
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            // Single instance check
            bool createdNew;
            _mutex = new Mutex(true, "GlassBar.Dashboard", out createdNew);

            if (!createdNew)
            {
                // Another instance is running - bring it to foreground
                BringExistingInstanceToForeground();
                Environment.Exit(0);
                return;
            }

            // If launched via the "Run at startup" registry entry, start hidden in tray
            bool startHidden = args.Arguments.Contains("/autostart", StringComparison.OrdinalIgnoreCase)
                            || Environment.GetCommandLineArgs().Any(
                                   a => a.Equals("/autostart", StringComparison.OrdinalIgnoreCase));

            _window = new MainWindow(startHidden);
            _window.Activate();
        }

        private void BringExistingInstanceToForeground()
        {
            try
            {
                var currentProcess = Process.GetCurrentProcess();
                var processes = Process.GetProcessesByName(currentProcess.ProcessName);

                foreach (var process in processes)
                {
                    if (process.Id == currentProcess.Id) continue;

                    // MainWindowHandle is IntPtr.Zero for hidden windows (IsWindowVisible=false),
                    // so fall back to enumerating all windows by process ID.
                    var hwnd = process.MainWindowHandle != IntPtr.Zero
                        ? process.MainWindowHandle
                        : FindWindowByProcessId((uint)process.Id);

                    if (hwnd == IntPtr.Zero) continue;

                    if (!IsWindowVisible(hwnd))
                        ShowWindow(hwnd, SW_SHOW);      // unhide (was started via /autostart)
                    else if (IsIconic(hwnd))
                        ShowWindow(hwnd, SW_RESTORE);   // un-minimize

                    SetForegroundWindow(hwnd);
                    break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to bring existing instance to foreground: {ex.Message}");
            }
        }

        private static IntPtr FindWindowByProcessId(uint targetPid)
        {
            IntPtr found = IntPtr.Zero;
            EnumWindows((hwnd, _) =>
            {
                GetWindowThreadProcessId(hwnd, out uint pid);
                if (pid == targetPid) { found = hwnd; return false; }
                return true;
            }, IntPtr.Zero);
            return found;
        }
    }
}
