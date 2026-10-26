using System;
using System.IO;
using System.Text.Json;
using System.Windows.Media.Imaging;

namespace CodePin;

public class AppSettings
{
    public double Left { get; set; } = double.NaN;
    public double Top { get; set; } = double.NaN;
    public double Width { get; set; } = 620;
    public double Height { get; set; } = 420;
    public double Opacity { get; set; } = 1.0;
    public bool Topmost { get; set; } = true;
    public bool HasSavedImage { get; set; } = false;
    public double Zoom { get; set; } = 1.0;
    public double PanX { get; set; } = 0;
    public double PanY { get; set; } = 0;
    public bool IsFirstRun { get; set; } = true;
}

public static class SettingsManager
{
    private static readonly string AppDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
        "CodePin"
    );
    private static readonly string ConfigPath = Path.Combine(AppDir, "config.json");
    private static readonly string ImageCachePath = Path.Combine(AppDir, "saved_session.png");

    static SettingsManager()
    {
        try
        {
            if (!Directory.Exists(AppDir))
            {
                Directory.CreateDirectory(AppDir);
            }
        }
        catch
        {
            // Directory creation error handled
        }
    }

    public static AppSettings LoadSettings()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                string json = File.ReadAllText(ConfigPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings != null) return settings;
            }
        }
        catch
        {
            // Fallback to default
        }
        return new AppSettings();
    }

    public static void SaveSettings(AppSettings settings)
    {
        try
        {
            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
        catch
        {
            // Silently ignore save failures on close
        }
    }

    public static void SaveSessionImage(BitmapSource bitmap)
    {
        try
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = new FileStream(ImageCachePath, FileMode.Create, FileAccess.Write);
            encoder.Save(stream);
        }
        catch
        {
            // Silently ignore cache write errors
        }
    }

    public static BitmapImage? LoadSessionImage()
    {
        try
        {
            if (File.Exists(ImageCachePath))
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(ImageCachePath, UriKind.Absolute);
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
        }
        catch
        {
            // If corrupt image, ignore
        }
        return null;
    }

    public static void ClearSessionImage()
    {
        try
        {
            if (File.Exists(ImageCachePath))
            {
                File.Delete(ImageCachePath);
            }
        }
        catch
        {
            // Ignore delete error
        }
    }
}
