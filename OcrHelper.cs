using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace CodePin;

public static class OcrHelper
{
    public static async Task<string> ExtractTextFromBitmapSourceAsync(BitmapSource sourceBitmap)
    {
        try
        {
            // 1. Calculate optimal upscale factor
            // IDE screenshots typically have small font sizes (11-13px).
            // Upscaling 2x-2.5x dramatically enhances character edge detection for symbols (; : { } [ ] =>).
            double scale = 1.0;
            if (sourceBitmap.PixelWidth < 2200 && sourceBitmap.PixelHeight < 2200)
            {
                scale = Math.Min(2.5, Math.Max(1.6, 2200.0 / Math.Max(sourceBitmap.PixelWidth, sourceBitmap.PixelHeight)));
            }

            BitmapSource bitmapToProcess = sourceBitmap;
            if (Math.Abs(scale - 1.0) > 0.1)
            {
                var scaled = new TransformedBitmap();
                scaled.BeginInit();
                scaled.Source = sourceBitmap;
                scaled.Transform = new ScaleTransform(scale, scale);
                scaled.EndInit();
                scaled.Freeze();
                bitmapToProcess = scaled;
            }

            // 2. Encode to PNG memory bytes
            byte[] pngBytes;
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmapToProcess));
            using (var ms = new MemoryStream())
            {
                encoder.Save(ms);
                pngBytes = ms.ToArray();
            }

            // 3. Load into IRandomAccessStream and SoftwareBitmap
            using var msForOcr = new MemoryStream(pngBytes);
            using var ras = msForOcr.AsRandomAccessStream();

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

                // Priority 1: English (ideal for programming language keywords and syntax)
                var enLang = new Windows.Globalization.Language("en-US");
                if (OcrEngine.IsLanguageSupported(enLang))
                {
                    engine = OcrEngine.TryCreateFromLanguage(enLang);
                }

                // Priority 2: User profile languages (matches OS settings)
                if (engine == null)
                {
                    engine = OcrEngine.TryCreateFromUserProfileLanguages();
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

                // 6. Format with Indentation & Code Cleanups
                return FormatOcrResultForCode(ocrResult);
            }
        }
        catch (Exception ex)
        {
            throw new Exception(ex.Message, ex);
        }
    }

    private static string FormatOcrResultForCode(OcrResult ocrResult)
    {
        var validLines = ocrResult.Lines.Where(l => l.Words.Count > 0).ToList();
        if (validLines.Count == 0) return string.Empty;

        // Estimate base left margin (minX)
        double minX = validLines.Min(l => l.Words[0].BoundingRect.X);

        // Estimate average character width across all words
        double totalWidth = 0;
        int totalChars = 0;
        foreach (var l in validLines)
        {
            foreach (var w in l.Words)
            {
                if (w.Text.Length > 0 && w.BoundingRect.Width > 0)
                {
                    totalWidth += w.BoundingRect.Width;
                    totalChars += w.Text.Length;
                }
            }
        }

        double avgCharWidth = totalChars > 0 ? (totalWidth / totalChars) : 10.0;
        if (avgCharWidth < 3.0) avgCharWidth = 10.0;

        var sb = new StringBuilder();

        for (int lineIdx = 0; lineIdx < ocrResult.Lines.Count; lineIdx++)
        {
            var line = ocrResult.Lines[lineIdx];
            if (line.Words.Count == 0)
            {
                sb.AppendLine();
                continue;
            }

            double lineStartX = line.Words[0].BoundingRect.X;
            double indentPixels = Math.Max(0, lineStartX - minX);
            int spaceCount = (int)Math.Round(indentPixels / avgCharWidth);

            // Prepend indentation spaces
            if (spaceCount > 0)
            {
                sb.Append(new string(' ', spaceCount));
            }

            // Append words with reconstructed horizontal gaps
            for (int wIdx = 0; wIdx < line.Words.Count; wIdx++)
            {
                var word = line.Words[wIdx];
                if (wIdx > 0)
                {
                    var prevWord = line.Words[wIdx - 1];
                    double gap = word.BoundingRect.X - (prevWord.BoundingRect.X + prevWord.BoundingRect.Width);
                    int gapSpaces = (int)Math.Max(1, Math.Round(gap / avgCharWidth));
                    gapSpaces = Math.Min(gapSpaces, 6);
                    sb.Append(new string(' ', gapSpaces));
                }

                string cleanWord = CleanCodeWord(word.Text);
                sb.Append(cleanWord);
            }

            sb.AppendLine();
        }

        return CleanOverallCodeText(sb.ToString());
    }

    private static string CleanCodeWord(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        // Normalize fullwidth ASCII variants (commonly produced by Asian Windows OCR packs)
        var chars = text.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            char c = chars[i];
            if (c >= 0xFF01 && c <= 0xFF5E)
            {
                chars[i] = (char)(c - 0xFEE0);
            }
        }
        string result = new string(chars);

        // Replace smart quotes, typographical dashes with standard code equivalents
        return result
            .Replace('“', '"')
            .Replace('”', '"')
            .Replace('„', '"')
            .Replace('‟', '"')
            .Replace('‘', '\'')
            .Replace('’', '\'')
            .Replace('‚', '\'')
            .Replace('‛', '\'')
            .Replace('—', '-')
            .Replace('–', '-')
            .Replace('−', '-')
            .Replace('\u00A0', ' '); // Non-breaking space to regular space
    }

    private static string CleanOverallCodeText(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        return text
            .Replace("…", "...")
            .Replace("\r\n", "\n")
            .Replace("\r", "\n")
            .Replace("\n", Environment.NewLine);
    }
}
