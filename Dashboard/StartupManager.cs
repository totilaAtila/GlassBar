using Microsoft.Win32;
using System;
using System.Diagnostics;

namespace CrystalFrame.Dashboard
{
    public static class StartupManager
    {
        private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "CrystalFrame";

        public static bool IsEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey);
                return key?.GetValue(AppName) != null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Startup] IsEnabled error: {ex.Message}");
                return false;
            }
        }

        public static void SetEnabled(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
                if (key == null)
                {
                    Debug.WriteLine("[Startup] Could not open Run registry key");
                    return;
                }

                if (enable)
                {
                    var path = Environment.ProcessPath
                               ?? Process.GetCurrentProcess().MainModule?.FileName;
                    if (path != null)
                    {
                        key.SetValue(AppName, $"\"{path}\" /autostart");
                        Debug.WriteLine($"[Startup] Registered: \"{path}\" /autostart");
                    }
                }
                else
                {
                    key.DeleteValue(AppName, throwOnMissingValue: false);
                    Debug.WriteLine("[Startup] Removed from startup");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Startup] SetEnabled error: {ex.Message}");
            }
        }
    }
}
