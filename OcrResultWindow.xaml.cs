using System;
using System.Windows;
using System.Windows.Input;

namespace CodePin;

public partial class OcrResultWindow : Window
{
    public OcrResultWindow(string extractedText)
    {
        InitializeComponent();
        TxtCode.Text = extractedText;

        int lineCount = extractedText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).Length;
        int charCount = extractedText.Length;
        TxtStats.Text = $"({lineCount} dòng, {charCount} ký tự - Đã tự động giữ thụt lề)";
    }

    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(TxtCode.Text);
            TxtStatus.Text = "✅ Đã sao chép toàn bộ nội dung trong ô vào Clipboard!";
            TxtStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(166, 227, 161));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không thể copy: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not System.Windows.Controls.TextBox)
        {
            try
            {
                DragMove();
            }
            catch { }
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
