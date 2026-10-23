using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace CodePin;

public partial class MainWindow : Window
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WM_NCLBUTTONDOWN = 0xA1;
    private const int HTBOTTOMRIGHT = 17;

    // Win32 API
    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const int HOTKEY_CLICKTHROUGH_F8 = 9001;
    private const int HOTKEY_CLICKTHROUGH_P = 9002;
    private const int HOTKEY_WINDOW_PIN_T = 9003;

    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint VK_P = 0x50; // 'P'
    private const uint VK_T = 0x54; // 'T'
    private const uint VK_F8 = 0x77; // F8

    private bool _isClickThrough = false;
    private IntPtr _hwnd = IntPtr.Zero;
    private HwndSource? _hwndSource;
    private DispatcherTimer? _toastTimer;

    // Pan state
    private Point _panStart;
    private bool _isPanning = false;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        UpdateStatusText();
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        _hwndSource = HwndSource.FromHwnd(_hwnd);
        _hwndSource?.AddHook(HwndHook);

        // Register Global Hotkeys
        RegisterHotKey(_hwnd, HOTKEY_CLICKTHROUGH_F8, 0, VK_F8);
        RegisterHotKey(_hwnd, HOTKEY_CLICKTHROUGH_P, MOD_CONTROL | MOD_ALT, VK_P);
        RegisterHotKey(_hwnd, HOTKEY_WINDOW_PIN_T, MOD_CONTROL | MOD_ALT, VK_T);

        // 1. Restore previous session if available
        RestoreSession();
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Save current session before exiting
        SaveSession();

        if (_hwnd != IntPtr.Zero)
        {
            UnregisterHotKey(_hwnd, HOTKEY_CLICKTHROUGH_F8);
            UnregisterHotKey(_hwnd, HOTKEY_CLICKTHROUGH_P);
            UnregisterHotKey(_hwnd, HOTKEY_WINDOW_PIN_T);
        }
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_HOTKEY = 0x0312;
        if (msg == WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if (id == HOTKEY_CLICKTHROUGH_F8 || id == HOTKEY_CLICKTHROUGH_P)
            {
                ToggleClickThrough();
                handled = true;
            }
            else if (id == HOTKEY_WINDOW_PIN_T)
            {
                HandlePinActiveWindowHotkey();
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    #region Session Restore & Save

    private void RestoreSession()
    {
        try
        {
            var settings = SettingsManager.LoadSettings();

            // Restore bounds if valid
            if (!double.IsNaN(settings.Left) && !double.IsNaN(settings.Top))
            {
                double maxLeft = SystemParameters.VirtualScreenWidth - 100;
                double maxTop = SystemParameters.VirtualScreenHeight - 100;

                if (settings.Left < maxLeft && settings.Top < maxTop && settings.Left >= 0 && settings.Top >= 0)
                {
                    this.Left = settings.Left;
                    this.Top = settings.Top;
                }
            }

            if (settings.Width >= MinWidth) this.Width = settings.Width;
            if (settings.Height >= MinHeight) this.Height = settings.Height;

            this.Opacity = Math.Clamp(settings.Opacity, 0.20, 1.0);
            this.Topmost = settings.Topmost;

            // Restore saved image
            if (settings.HasSavedImage)
            {
                var cachedImage = SettingsManager.LoadSessionImage();
                if (cachedImage != null)
                {
                    PinnedImage.Source = cachedImage;
                    EmptyStatePanel.Visibility = Visibility.Collapsed;
                    ImageContainer.Visibility = Visibility.Visible;

                    ImageScale.ScaleX = settings.Zoom > 0.1 ? settings.Zoom : 1.0;
                    ImageScale.ScaleY = settings.Zoom > 0.1 ? settings.Zoom : 1.0;
                    ImageTranslate.X = settings.PanX;
                    ImageTranslate.Y = settings.PanY;

                    UpdateStatusText();
                    ShowToast("🔄 Đã khôi phục phiên làm việc trước đó");
                    return;
                }
            }

            // If no saved image, try pasting from clipboard on startup
            TryPasteImage();
        }
        catch
        {
            // Fallback gracefully
        }
    }

    private void SaveSession()
    {
        try
        {
            var settings = new AppSettings
            {
                Left = this.Left,
                Top = this.Top,
                Width = this.Width,
                Height = this.Height,
                Opacity = this.Opacity,
                Topmost = this.Topmost,
                Zoom = ImageScale.ScaleX,
                PanX = ImageTranslate.X,
                PanY = ImageTranslate.Y,
                HasSavedImage = PinnedImage.Source is BitmapSource
            };

            SettingsManager.SaveSettings(settings);

            if (PinnedImage.Source is BitmapSource bs)
            {
                SettingsManager.SaveSessionImage(bs);
            }
            else
            {
                SettingsManager.ClearSessionImage();
            }
        }
        catch
        {
            // Silently ignore save errors during shutdown
        }
    }

    #endregion

    #region Window Pinning (Any Application)

    private void HandlePinActiveWindowHotkey()
    {
        IntPtr fg = WindowHelper.GetForegroundWindow();
        if (fg == IntPtr.Zero) return;

        if (fg == _hwnd)
        {
            // Toggle CodePin itself
            Topmost = !Topmost;
            UpdateStatusText();
            ShowToast(Topmost ? "📌 CodePin: Đã bật Always-on-top" : "🔓 CodePin: Đã tắt Always-on-top");
        }
        else
        {
            // Toggle external application window
            if (WindowHelper.ToggleWindowTopmost(fg, out bool isTop, out string title))
            {
                string shortTitle = string.IsNullOrWhiteSpace(title) ? "Cửa sổ" : (title.Length > 28 ? title.Substring(0, 25) + "..." : title);
                ShowToast(isTop ? $"📌 Đã ghim: {shortTitle}" : $"🔓 Đã bỏ ghim: {shortTitle}");
            }
        }
    }

    private void MenuWindowPicker_Click(object sender, RoutedEventArgs e)
    {
        var picker = new WindowPickerWindow(_hwnd)
        {
            Owner = this
        };
        picker.ShowDialog();
    }

    #endregion

    #region OCR (Extract Code / Text)

    private async void MenuOcr_Click(object sender, RoutedEventArgs e)
    {
        await PerformOcrAsync();
    }

    private async Task PerformOcrAsync()
    {
        if (PinnedImage.Source is not BitmapSource bitmap)
        {
            ShowToast("⚠️ Chưa có ảnh nào để trích xuất!");
            return;
        }

        ShowToast("⏳ Đang trích xuất code bằng Windows OCR...", 5000);

        try
        {
            string extracted = await OcrHelper.ExtractTextFromBitmapSourceAsync(bitmap);

            if (string.IsNullOrWhiteSpace(extracted))
            {
                ShowToast("⚠️ Không nhận diện được văn bản trong ảnh!");
                return;
            }

            Clipboard.SetText(extracted);
            int lineCount = extracted.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).Length;
            ShowToast($"✅ Đã copy {lineCount} dòng code vào Clipboard!");

            // Open Preview / Editor window so user can inspect and tweak code immediately
            var resultWindow = new OcrResultWindow(extracted)
            {
                Owner = this
            };
            resultWindow.ShowDialog();
        }
        catch (Exception ex)
        {
            ShowToast($"❌ Lỗi OCR: {ex.Message}");
        }
    }

    #endregion

    #region Toast Notification

    private void ShowToast(string message, int durationMs = 3200)
    {
        TxtToast.Text = message;
        ToastNotification.Visibility = Visibility.Visible;

        _toastTimer?.Stop();
        _toastTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(durationMs)
        };
        _toastTimer.Tick += (s, e) =>
        {
            ToastNotification.Visibility = Visibility.Collapsed;
            _toastTimer.Stop();
        };
        _toastTimer.Start();
    }

    #endregion

    #region Image Loading & Pasting

    private bool TryPasteImage()
    {
        try
        {
            if (Clipboard.ContainsImage())
            {
                BitmapSource? image = Clipboard.GetImage();
                if (image != null)
                {
                    LoadBitmapSource(image);
                    ShowToast("📋 Đã dán ảnh từ Clipboard!");
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không thể đọc ảnh từ Clipboard: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        return false;
    }

    private void LoadBitmapSource(BitmapSource bitmap)
    {
        PinnedImage.Source = bitmap;
        EmptyStatePanel.Visibility = Visibility.Collapsed;
        ImageContainer.Visibility = Visibility.Visible;

        // Auto-scale window to reasonable bounds keeping aspect ratio
        double naturalWidth = bitmap.PixelWidth;
        double naturalHeight = bitmap.PixelHeight;

        if (naturalWidth > 0 && naturalHeight > 0)
        {
            double screenW = SystemParameters.PrimaryScreenWidth * 0.75;
            double screenH = SystemParameters.PrimaryScreenHeight * 0.75;

            double scale = Math.Min(1.0, Math.Min(screenW / naturalWidth, screenH / naturalHeight));
            double targetW = Math.Max(260, naturalWidth * scale);
            double targetH = Math.Max(160, (targetW / naturalWidth) * naturalHeight);

            this.Width = targetW + 24;
            this.Height = targetH + 68;
        }

        ResetTransform();
        UpdateStatusText();

        // Cache image immediately for session
        SettingsManager.SaveSessionImage(bitmap);
    }

    private void LoadImageFromFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return;

            BitmapImage bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(filePath, UriKind.Absolute);
            bmp.EndInit();
            bmp.Freeze();

            LoadBitmapSource(bmp);
            ShowToast("📁 Đã mở ảnh thành công!");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không thể mở file ảnh: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    #endregion

    #region Window Drag, Pan, Zoom & Resize

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_isClickThrough) return;

        // Space + Left Click is used for panning
        if (Keyboard.IsKeyDown(Key.Space))
        {
            _isPanning = true;
            _panStart = e.GetPosition(this);
            Mouse.Capture((IInputElement)sender);
            Cursor = Cursors.Hand;
            return;
        }

        // Avoid drag if clicked on a button or resize grip
        if (e.OriginalSource is DependencyObject dep && FindParent<Button>(dep) != null) return;
        if (e.OriginalSource is ResizeGrip) return;

        if (e.LeftButton == MouseButtonState.Pressed)
        {
            try
            {
                DragMove();
            }
            catch
            {
                // Ignore any state issues
            }
        }
    }

    private void Image_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Double-click image to reset zoom & pan
        if (e.ClickCount == 2)
        {
            ResetTransform();
            e.Handled = true;
        }
    }

    private void Window_MouseDown(object sender, MouseButtonEventArgs e)
    {
        // Middle button to Pan
        if (e.MiddleButton == MouseButtonState.Pressed)
        {
            _isPanning = true;
            _panStart = e.GetPosition(this);
            Mouse.Capture((IInputElement)sender);
            Cursor = Cursors.Hand;
        }
    }

    private void Window_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isPanning)
        {
            if (e.MiddleButton == MouseButtonState.Pressed || (Keyboard.IsKeyDown(Key.Space) && e.LeftButton == MouseButtonState.Pressed))
            {
                Point current = e.GetPosition(this);
                Vector diff = current - _panStart;
                ImageTranslate.X += diff.X;
                ImageTranslate.Y += diff.Y;
                _panStart = current;
            }
        }
    }

    private void Window_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isPanning)
        {
            if (e.MiddleButton == MouseButtonState.Released && e.LeftButton == MouseButtonState.Released)
            {
                _isPanning = false;
                Mouse.Capture(null);
                Cursor = Cursors.Arrow;
            }
        }
    }

    private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            // Shift + Wheel: Adjust Opacity
            double delta = e.Delta > 0 ? 0.05 : -0.05;
            double newOpacity = Math.Clamp(this.Opacity + delta, 0.20, 1.0);
            this.Opacity = newOpacity;
            UpdateStatusText();
        }
        else
        {
            // Normal Wheel: Zoom in / Zoom out
            double zoomFactor = e.Delta > 0 ? 1.15 : 0.87;
            double newScaleX = ImageScale.ScaleX * zoomFactor;

            if (newScaleX >= 0.2 && newScaleX <= 10.0)
            {
                ImageScale.ScaleX = newScaleX;
                ImageScale.ScaleY = newScaleX;
                UpdateStatusText();
            }
        }
        e.Handled = true;
    }

    private void ResizeGrip_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && _hwnd != IntPtr.Zero)
        {
            SendMessage(_hwnd, WM_NCLBUTTONDOWN, (IntPtr)HTBOTTOMRIGHT, IntPtr.Zero);
            e.Handled = true;
        }
    }

    private void ResetTransform()
    {
        ImageScale.ScaleX = 1.0;
        ImageScale.ScaleY = 1.0;
        ImageTranslate.X = 0;
        ImageTranslate.Y = 0;
        UpdateStatusText();
    }

    private void UpdateStatusText()
    {
        int zoomPercent = (int)Math.Round(ImageScale.ScaleX * 100);
        int opacityPercent = (int)Math.Round(this.Opacity * 100);
        TxtInfo.Text = $"{zoomPercent}% | Mờ: {opacityPercent}%";

        TxtPinIndicator.Text = Topmost ? "📌 ON TOP" : "🔓 NORMAL";
        TxtPinIndicator.Foreground = Topmost 
            ? new SolidColorBrush(Color.FromRgb(166, 227, 161)) 
            : new SolidColorBrush(Color.FromRgb(166, 173, 200));

        if (MenuTopmostItem != null)
        {
            MenuTopmostItem.IsChecked = Topmost;
        }
    }

    private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        DependencyObject? parentObject = VisualTreeHelper.GetParent(child);
        if (parentObject == null) return null;
        if (parentObject is T parent) return parent;
        return FindParent<T>(parentObject);
    }

    #endregion

    #region Drag & Drop and Hotkeys

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[]? files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0)
            {
                string ext = Path.GetExtension(files[0]).ToLowerInvariant();
                string[] validExtensions = [".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp", ".ico", ".tiff"];
                if (Array.Exists(validExtensions, x => x == ext))
                {
                    LoadImageFromFile(files[0]);
                }
                else
                {
                    MessageBox.Show("Vui lòng thả file hình ảnh (PNG, JPG, BMP, WEBP, ...)", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }
    }

    private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Ctrl + V: Paste
        if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            if (!TryPasteImage())
            {
                ShowToast("Clipboard hiện không có ảnh nào!");
            }
            e.Handled = true;
            return;
        }

        // Ctrl + T: OCR Code
        if (e.Key == Key.T && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            await PerformOcrAsync();
            e.Handled = true;
            return;
        }

        // Ctrl + O: Open file
        if (e.Key == Key.O && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            OpenFile();
            e.Handled = true;
            return;
        }

        // Ctrl + 0: Reset zoom
        if (e.Key == Key.D0 && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            ResetTransform();
            e.Handled = true;
            return;
        }

        // Esc: Close
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }
    }

    #endregion

    #region Actions & Menu Handlers

    private void ContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (MenuTopmostItem != null)
        {
            MenuTopmostItem.IsChecked = Topmost;
        }
    }

    private void MenuPaste_Click(object sender, RoutedEventArgs e)
    {
        if (!TryPasteImage())
        {
            MessageBox.Show("Không có dữ liệu ảnh trong Clipboard.\nHãy dùng Win + Shift + S chụp lại rồi bấm lại nút này!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void MenuOpenFile_Click(object sender, RoutedEventArgs e)
    {
        OpenFile();
    }

    private void OpenFile()
    {
        OpenFileDialog dialog = new OpenFileDialog
        {
            Filter = "Ảnh (*.png;*.jpg;*.jpeg;*.bmp;*.webp)|*.png;*.jpg;*.jpeg;*.bmp;*.webp|Tất cả file (*.*)|*.*",
            Title = "Chọn file ảnh để ghim"
        };

        if (dialog.ShowDialog() == true)
        {
            LoadImageFromFile(dialog.FileName);
        }
    }

    private void MenuTopmost_Click(object sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        UpdateStatusText();
    }

    private void BtnToggleTopmost_Click(object sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        UpdateStatusText();
    }

    private void MenuResetZoom_Click(object sender, RoutedEventArgs e)
    {
        ResetTransform();
    }

    private void MenuOpacity_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && item.Tag is string valStr && double.TryParse(valStr, out double opacityVal))
        {
            this.Opacity = opacityVal;
            UpdateStatusText();
        }
    }

    private void MenuClickThrough_Click(object sender, RoutedEventArgs e)
    {
        ToggleClickThrough();
    }

    private void ToggleClickThrough()
    {
        if (_hwnd == IntPtr.Zero) return;

        _isClickThrough = !_isClickThrough;
        int currentStyle = GetWindowLong(_hwnd, GWL_EXSTYLE);

        if (_isClickThrough)
        {
            SetWindowLong(_hwnd, GWL_EXSTYLE, currentStyle | WS_EX_TRANSPARENT);
            MessageBox.Show("Đã BẬT chế độ Xuyên chuột (Click-Through)!\nChuột sẽ click xuyên qua ảnh xuống IDE bên dưới.\n\n👉 Nhấn F8 hoặc Ctrl+Alt+P để TẮT.", "Chế độ Xuyên Chuột", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            SetWindowLong(_hwnd, GWL_EXSTYLE, currentStyle & ~WS_EX_TRANSPARENT);
            ShowToast("Đã TẮT chế độ Xuyên chuột.");
        }
    }

    private void MenuClearSession_Click(object sender, RoutedEventArgs e)
    {
        PinnedImage.Source = null;
        ImageContainer.Visibility = Visibility.Collapsed;
        EmptyStatePanel.Visibility = Visibility.Visible;
        SettingsManager.ClearSessionImage();
        ResetTransform();
        ShowToast("🗑️ Đã làm mới phiên!");
    }

    private void MenuClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    #endregion
}
