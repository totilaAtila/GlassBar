using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Graphics;
using Windows.UI;
using WinRT.Interop;

namespace CrystalFrame.Dashboard
{
    public sealed partial class MainWindow : Window
    {
        private MainViewModel _viewModel;
        private bool _isInitialized = false;

        public MainWindow()
        {
            InitializeComponent();

            // Set window size and icon
            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            appWindow.Resize(new SizeInt32(500, 600));

            // Set window icon
            var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "app.ico");
            if (System.IO.File.Exists(iconPath))
            {
                appWindow.SetIcon(iconPath);
            }

            _viewModel = new MainViewModel();
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;

            // Handle Dashboard closing - Core behavior depends on toggle state
            this.Closed += OnWindowClosed;

            _ = InitializeAsync();
        }

        private async void OnWindowClosed(object sender, WindowEventArgs e)
        {
            try
            {
                await _viewModel.OnDashboardClosingAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during Dashboard close: {ex.Message}");
            }
        }

        private async Task InitializeAsync()
        {
            try
            {
                var success = await _viewModel.InitializeAsync();

                if (!success && !string.IsNullOrEmpty(_viewModel.ExtractionError))
                {
                    // Show extraction error dialog
                    await ShowExtractionErrorDialogAsync(_viewModel.ExtractionError);
                }

                // Update UI with loaded config - wrap in try/catch to handle partial failures
                try
                {
                    TaskbarOpacitySlider.Value = _viewModel.TaskbarOpacity;
                    StartOpacitySlider.Value = _viewModel.StartOpacity;
                    TaskbarEnabledToggle.IsOn = _viewModel.TaskbarEnabled;
                    StartEnabledToggle.IsOn = _viewModel.StartEnabled;
                    CoreRunningToggle.IsOn = _viewModel.CoreRunning;

                    TaskbarColorRSlider.Value = _viewModel.TaskbarColorR;
                    TaskbarColorGSlider.Value = _viewModel.TaskbarColorG;
                    TaskbarColorBSlider.Value = _viewModel.TaskbarColorB;

                    StartBgColorRSlider.Value = _viewModel.StartBgColorR;
                    StartBgColorGSlider.Value = _viewModel.StartBgColorG;
                    StartBgColorBSlider.Value = _viewModel.StartBgColorB;

                    StartTextColorRSlider.Value = _viewModel.StartTextColorR;
                    StartTextColorGSlider.Value = _viewModel.StartTextColorG;
                    StartTextColorBSlider.Value = _viewModel.StartTextColorB;

                    StartShowControlPanel.IsChecked = _viewModel.StartShowControlPanel;
                    StartShowDeviceManager.IsChecked = _viewModel.StartShowDeviceManager;
                    StartShowInstalledApps.IsChecked = _viewModel.StartShowInstalledApps;
                    StartShowDocuments.IsChecked = _viewModel.StartShowDocuments;
                    StartShowPictures.IsChecked = _viewModel.StartShowPictures;
                    StartShowVideos.IsChecked = _viewModel.StartShowVideos;
                    StartShowRecentFiles.IsChecked = _viewModel.StartShowRecentFiles;

                    UpdateOpacityText();
                    UpdateStatus();
                    UpdateCoreStatus();
                }
                catch (Exception uiEx)
                {
                    Debug.WriteLine($"UI update failed: {uiEx.Message}, but continuing...");
                    // Don't block initialization for UI update failures
                }

                // IMPORTANT: Set _isInitialized = true even if Core connection failed
                // This allows UI controls to work for manual retry
                _isInitialized = true;

                Debug.WriteLine($"[INIT] Dashboard initialized successfully, _isInitialized={_isInitialized}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Dashboard initialization failed: {ex.Message}\n{ex.StackTrace}");
                ConnectionStatusText.Text = $"✗ Initialization failed: {ex.Message}";

                // Still enable UI for manual control even after initialization failure
                _isInitialized = true;
            }
        }

        private async Task ShowExtractionErrorDialogAsync(string errorMessage)
        {
            // Wait for XamlRoot to be available
            if (this.Content.XamlRoot == null)
            {
                var tcs = new TaskCompletionSource<bool>();
                if (this.Content is FrameworkElement fe)
                {
                    fe.Loaded += (s, e) => tcs.TrySetResult(true);
                }
                else
                {
                    tcs.TrySetResult(true); // Fallback, proceed anyway
                }
                await tcs.Task;
            }

            var dialog = new ContentDialog
            {
                Title = "Core Engine Extraction Failed",
                Content = $"Could not extract the Core engine:\n\n{errorMessage}\n\nPlease try reinstalling CrystalFrame or check your antivirus settings.",
                CloseButtonText = "OK",
                XamlRoot = this.Content.XamlRoot
            };

            await dialog.ShowAsync();
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                UpdateStatus();
                
                if (e.PropertyName == nameof(MainViewModel.CoreRunning))
                {
                    UpdateCoreStatus();
                    // Sync toggle without re-triggering event
                    if (CoreRunningToggle.IsOn != _viewModel.CoreRunning)
                    {
                        _isInitialized = false;
                        CoreRunningToggle.IsOn = _viewModel.CoreRunning;
                        _isInitialized = true;
                    }
                }
            });
        }

        private async void CoreRunning_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;

            try
            {
                await _viewModel.SetCoreRunningAsync(CoreRunningToggle.IsOn);
                UpdateCoreStatus();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to toggle Core: {ex.Message}");
            }
        }

        private async void TaskbarEnabled_Toggled(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine($"[UI] TaskbarEnabled_Toggled fired: value={TaskbarEnabledToggle.IsOn}, _isInitialized={_isInitialized}");

            if (!_isInitialized)
            {
                Debug.WriteLine("[UI] Skipping because _isInitialized=false");
                return;
            }

            try
            {
                Debug.WriteLine($"[UI] Calling ViewModel.SetTaskbarEnabledAsync({TaskbarEnabledToggle.IsOn})");
                await _viewModel.SetTaskbarEnabledAsync(TaskbarEnabledToggle.IsOn);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to toggle taskbar: {ex.Message}");
            }
        }

        private void TaskbarOpacity_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            Debug.WriteLine($"[UI] TaskbarOpacity_ValueChanged fired: value={e.NewValue}, _isInitialized={_isInitialized}");

            // Update text display always, even during initialization
            int value = (int)e.NewValue;
            TaskbarOpacityValue.Text = value.ToString();

            if (!_isInitialized)
            {
                Debug.WriteLine("[UI] Skipping IPC because _isInitialized=false");
                DebugStatusText.Text = $"[SKIP] _isInitialized=false";
                return;
            }

            DebugStatusText.Text = $"[SLIDER] Sending opacity={value}, init={_isInitialized}";
            Debug.WriteLine($"[UI] Calling ViewModel.OnTaskbarOpacityChanged({value})");

            try
            {
                _viewModel.OnTaskbarOpacityChanged(value);
                DebugStatusText.Text = $"[OK] Sent opacity={value}";
            }
            catch (Exception ex)
            {
                DebugStatusText.Text = $"[ERROR] {ex.Message}";
            }
        }

        private async void StartEnabled_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;

            try
            {
                await _viewModel.SetStartEnabledAsync(StartEnabledToggle.IsOn);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to toggle start: {ex.Message}");
            }
        }

        private void StartOpacity_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (!_isInitialized) return;

            int value = (int)e.NewValue;
            StartOpacityValue.Text = value.ToString();

            _viewModel.OnStartOpacityChanged(value);
        }

        private void TaskbarColorR_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (!_isInitialized) return;
            int value = (int)e.NewValue;
            TaskbarColorRValue.Text = value.ToString();
            UpdateTaskbarColorPreview();
            _viewModel.OnTaskbarColorChanged(value, (int)TaskbarColorGSlider.Value, (int)TaskbarColorBSlider.Value);
        }

        private void TaskbarColorG_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (!_isInitialized) return;
            int value = (int)e.NewValue;
            TaskbarColorGValue.Text = value.ToString();
            UpdateTaskbarColorPreview();
            _viewModel.OnTaskbarColorChanged((int)TaskbarColorRSlider.Value, value, (int)TaskbarColorBSlider.Value);
        }

        private void TaskbarColorB_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (!_isInitialized) return;
            int value = (int)e.NewValue;
            TaskbarColorBValue.Text = value.ToString();
            UpdateTaskbarColorPreview();
            _viewModel.OnTaskbarColorChanged((int)TaskbarColorRSlider.Value, (int)TaskbarColorGSlider.Value, value);
        }

        private void UpdateTaskbarColorPreview()
        {
            var r = (byte)(int)TaskbarColorRSlider.Value;
            var g = (byte)(int)TaskbarColorGSlider.Value;
            var b = (byte)(int)TaskbarColorBSlider.Value;
            TaskbarColorPreview.Background = new SolidColorBrush(Color.FromArgb(255, r, g, b));
        }

        // Start Menu Background Color Handlers
        private void StartBgColorR_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (!_isInitialized) return;
            int value = (int)e.NewValue;
            StartBgColorRValue.Text = value.ToString();
            UpdateStartBgColorPreview();
            _viewModel.OnStartBgColorChanged(value, (int)StartBgColorGSlider.Value, (int)StartBgColorBSlider.Value);
        }

        private void StartBgColorG_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (!_isInitialized) return;
            int value = (int)e.NewValue;
            StartBgColorGValue.Text = value.ToString();
            UpdateStartBgColorPreview();
            _viewModel.OnStartBgColorChanged((int)StartBgColorRSlider.Value, value, (int)StartBgColorBSlider.Value);
        }

        private void StartBgColorB_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (!_isInitialized) return;
            int value = (int)e.NewValue;
            StartBgColorBValue.Text = value.ToString();
            UpdateStartBgColorPreview();
            _viewModel.OnStartBgColorChanged((int)StartBgColorRSlider.Value, (int)StartBgColorGSlider.Value, value);
        }

        private void UpdateStartBgColorPreview()
        {
            var r = (byte)(int)StartBgColorRSlider.Value;
            var g = (byte)(int)StartBgColorGSlider.Value;
            var b = (byte)(int)StartBgColorBSlider.Value;
            StartBgColorPreview.Background = new SolidColorBrush(Color.FromArgb(255, r, g, b));
        }

        // Start Menu Text Color Handlers
        private void StartTextColorR_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (!_isInitialized) return;
            int value = (int)e.NewValue;
            StartTextColorRValue.Text = value.ToString();
            UpdateStartTextColorPreview();
            _viewModel.OnStartTextColorChanged(value, (int)StartTextColorGSlider.Value, (int)StartTextColorBSlider.Value);
        }

        private void StartTextColorG_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (!_isInitialized) return;
            int value = (int)e.NewValue;
            StartTextColorGValue.Text = value.ToString();
            UpdateStartTextColorPreview();
            _viewModel.OnStartTextColorChanged((int)StartTextColorRSlider.Value, value, (int)StartTextColorBSlider.Value);
        }

        private void StartTextColorB_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (!_isInitialized) return;
            int value = (int)e.NewValue;
            StartTextColorBValue.Text = value.ToString();
            UpdateStartTextColorPreview();
            _viewModel.OnStartTextColorChanged((int)StartTextColorRSlider.Value, (int)StartTextColorGSlider.Value, value);
        }

        private void UpdateStartTextColorPreview()
        {
            var r = (byte)(int)StartTextColorRSlider.Value;
            var g = (byte)(int)StartTextColorGSlider.Value;
            var b = (byte)(int)StartTextColorBSlider.Value;
            StartTextColorPreview.Background = new SolidColorBrush(Color.FromArgb(255, r, g, b));
        }

        // Start Menu Items Handler
        private void StartMenuItem_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;

            var checkbox = sender as CheckBox;
            if (checkbox == null) return;

            bool isChecked = checkbox.IsChecked ?? false;
            string itemName = "";

            if (checkbox == StartShowControlPanel) itemName = "ControlPanel";
            else if (checkbox == StartShowDeviceManager) itemName = "DeviceManager";
            else if (checkbox == StartShowInstalledApps) itemName = "InstalledApps";
            else if (checkbox == StartShowDocuments) itemName = "Documents";
            else if (checkbox == StartShowPictures) itemName = "Pictures";
            else if (checkbox == StartShowVideos) itemName = "Videos";
            else if (checkbox == StartShowRecentFiles) itemName = "RecentFiles";

            if (!string.IsNullOrEmpty(itemName))
            {
                _viewModel.OnStartMenuItemChanged(itemName, isChecked);
                Debug.WriteLine($"[StartMenuItem] {itemName} = {isChecked}");
            }
        }

        private void UpdateOpacityText()
        {
            TaskbarOpacityValue.Text = ((int)TaskbarOpacitySlider.Value).ToString();
            StartOpacityValue.Text = ((int)StartOpacitySlider.Value).ToString();
        }

        private void UpdateStatus()
        {
            // Taskbar status
            if (_viewModel.TaskbarFound)
            {
                TaskbarStatusText.Text = "✓ Taskbar found";
                TaskbarStatusText.Foreground = new SolidColorBrush(Colors.Green);
            }
            else
            {
                TaskbarStatusText.Text = "⚠ Taskbar not detected";
                TaskbarStatusText.Foreground = new SolidColorBrush(Colors.Orange);
            }

            // Start status
            if (_viewModel.StartDetected)
            {
                StartStatusText.Text = "✓ Start menu detected";
                StartStatusText.Foreground = new SolidColorBrush(Colors.Green);
            }
            else
            {
                StartStatusText.Text = "⚠ Start menu not detected";
                StartStatusText.Foreground = new SolidColorBrush(Colors.Orange);
            }

            // Connection status
            ConnectionStatusText.Text = _viewModel.ConnectionStatus;
        }

        private void UpdateCoreStatus()
        {
            if (_viewModel.CoreRunning)
            {
                CoreStatusDot.Fill = new SolidColorBrush(Colors.LimeGreen);
                CoreStatusDetail.Text = "Running — overlay effects active";
            }
            else
            {
                CoreStatusDot.Fill = new SolidColorBrush(Colors.Gray);
                CoreStatusDetail.Text = "Stopped — no overlay effects";
            }
        }
    }
}
