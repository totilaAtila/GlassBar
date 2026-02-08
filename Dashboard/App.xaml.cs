using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace CrystalFrame.Dashboard
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

        private const int SW_RESTORE = 9;

        public App()
        {
            InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            // Single instance check
            bool createdNew;
            _mutex = new Mutex(true, "CrystalFrame.Dashboard", out createdNew);

            if (!createdNew)
            {
                // Another instance is running - bring it to foreground
                BringExistingInstanceToForeground();
                Environment.Exit(0);
                return;
            }

            _window = new MainWindow();
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
                    if (process.Id != currentProcess.Id && process.MainWindowHandle != IntPtr.Zero)
                    {
                        var hwnd = process.MainWindowHandle;

                        // Restore if minimized
                        if (IsIconic(hwnd))
                        {
                            ShowWindow(hwnd, SW_RESTORE);
                        }

                        // Bring to foreground
                        SetForegroundWindow(hwnd);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to bring existing instance to foreground: {ex.Message}");
            }
        }
    }
}
