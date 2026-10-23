using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;

namespace CodePin;

public partial class WindowPickerWindow : Window
{
    private List<WindowInfo> _allWindows = new List<WindowInfo>();
    private readonly IntPtr _parentHwnd;

    public WindowPickerWindow(IntPtr parentHwnd)
    {
        InitializeComponent();
        _parentHwnd = parentHwnd;
        Loaded += WindowPickerWindow_Loaded;
    }

    private void WindowPickerWindow_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshWindows();
    }

    private void RefreshWindows()
    {
        IntPtr myHwnd = new WindowInteropHelper(this).Handle;
        _allWindows = WindowHelper.GetOpenWindows(myHwnd)
            .Where(w => w.Hwnd != _parentHwnd)
            .OrderByDescending(w => w.IsTopmost)
            .ThenBy(w => w.Title)
            .ToList();

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        string query = TxtSearch.Text.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(query))
        {
            LstWindows.ItemsSource = _allWindows;
        }
        else
        {
            LstWindows.ItemsSource = _allWindows
                .Where(w => w.Title.ToLowerInvariant().Contains(query) || 
                            w.ProcessName.ToLowerInvariant().Contains(query))
                .ToList();
        }
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        TxtPlaceholder.Visibility = string.IsNullOrEmpty(TxtSearch.Text) ? Visibility.Visible : Visibility.Collapsed;
        ApplyFilter();
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        RefreshWindows();
    }

    private void BtnTogglePin_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is WindowInfo info)
        {
            if (WindowHelper.ToggleWindowTopmost(info.Hwnd, out bool isTop, out string title))
            {
                info.IsTopmost = isTop;
                // Re-bind to update UI
                int index = LstWindows.SelectedIndex;
                ApplyFilter();
                LstWindows.SelectedIndex = index;
            }
        }
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject dep && WindowHelper.IsWindowTopmost(new WindowInteropHelper(this).Handle))
        {
            try
            {
                DragMove();
            }
            catch { }
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
