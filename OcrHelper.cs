using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace CodePin;

public static class OcrHelper
{
    public static async Task<string> ExtractTextFromBitmapSourceAsync(BitmapSource bitmap)
    {
        try
        {
            // 1. Encode WPF BitmapSource to PNG memory bytes
            byte[] pngBytes;
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            using (var ms = new MemoryStream())
            {
                encoder.Save(ms);
                pngBytes = ms.ToArray();
            }

            // 2. Wrap MemoryStream as IRandomAccessStream without disposing prematurely
            using var msForOcr = new MemoryStream(pngBytes);
            using var ras = msForOcr.AsRandomAccessStream();

            // 3. Decode into Windows.Graphics.Imaging.SoftwareBitmap
            var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(ras);
            SoftwareBitmap rawSoftwareBitmap = await decoder.GetSoftwareBitmapAsync();

            // Convert to Bgra8 + Premultiplied alpha if needed (required by OcrEngine)
            SoftwareBitmap preparedBitmap = rawSoftwareBitmap;
            if (rawSoftwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 || 
                rawSoftwareBitmap.BitmapAlphaMode != BitmapAlphaMode.Premultiplied)
            {
                preparedBitmap = SoftwareBitmap.Convert(rawSoftwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                rawSoftwareBitmap.Dispose();
            }

            using (preparedBitmap)
            {
                // 4. Initialize Windows Media OCR Engine
                OcrEngine? engine = null;

                // Priority 1: User profile languages (matches OS settings)
                engine = OcrEngine.TryCreateFromUserProfileLanguages();

                // Priority 2: English (standard for code)
                if (engine == null)
                {
                    var enLang = new Windows.Globalization.Language("en-US");
                    if (OcrEngine.IsLanguageSupported(enLang))
                    {
                        engine = OcrEngine.TryCreateFromLanguage(enLang);
                    }
                }

                // Priority 3: Any available OCR language on the system
                if (engine == null && OcrEngine.AvailableRecognizerLanguages.Count > 0)
                {
                    engine = OcrEngine.TryCreateFromLanguage(OcrEngine.AvailableRecognizerLanguages[0]);
                }

                if (engine == null)
                {
                    throw new InvalidOperationException(
                        "Không tìm thấy gói ngôn ngữ OCR trên máy. Vui lòng cài đặt gói ngôn ngữ (English) trong Windows Settings > Time & Language."
                    );
                }

                // 5. Perform OCR
                var ocrResult = await engine.RecognizeAsync(preparedBitmap);
                if (ocrResult == null || ocrResult.Lines.Count == 0)
                {
                    return string.Empty;
                }

                var lines = ocrResult.Lines.Select(l => l.Text);
                return string.Join(Environment.NewLine, lines);
            }
        }
        catch (Exception ex)
        {
            throw new Exception(ex.Message, ex);
        }
    }
}
