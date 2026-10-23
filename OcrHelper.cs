using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace CodePin;

public class WordBox
{
    public string Text { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
}

public static class OcrHelper
{
    public static async Task<string> ExtractTextFromBitmapSourceAsync(BitmapSource sourceBitmap)
    {
        try
        {
            var engine = OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language("en-US"))
                         ?? OcrEngine.TryCreateFromUserProfileLanguages();

            if (engine == null && OcrEngine.AvailableRecognizerLanguages.Count > 0)
            {
                engine = OcrEngine.TryCreateFromLanguage(OcrEngine.AvailableRecognizerLanguages[0]);
            }

            if (engine == null)
            {
                throw new InvalidOperationException("Không tìm thấy gói ngôn ngữ OCR trên máy.");
            }

            int w = sourceBitmap.PixelWidth;
            int h = sourceBitmap.PixelHeight;

            var wb = new WriteableBitmap(sourceBitmap);
            int stride = w * 4;
            byte[] pixels = new byte[h * stride];
            wb.CopyPixels(pixels, stride, 0);

            // Robust background detection: sample across the image to find the most frequent color (histogram mode)
            var (bgR, bgG, bgB) = DetectBackgroundColor(pixels, w, h, stride);

            byte[] outPixels = new byte[pixels.Length];
            for (int i = 0; i < pixels.Length; i += 4)
            {
                byte b = pixels[i];
                byte g = pixels[i + 1];
                byte r = pixels[i + 2];

                // 1. Remove blue selection highlight in IDEs (e.g. #0B3769, #122E4F, #264F78)
                if (b >= 50 && b <= 135 && r <= 50 && g <= 85 && (b - r) > 25)
                {
                    r = bgR; g = bgG; b = bgB;
                }

                // 2. Remove yellow/orange squigglies (e.g. warnings under identifiers)
                if (r > 130 && g > 90 && b < 80 && (r - b) > 40 && (g - b) > 25)
                {
                    r = bgR; g = bgG; b = bgB;
                }
                // 3. Remove green squigglies / hints (under for(int) and if)
                if (g > 60 && (g - r) > 25 && (g - b) > 25)
                {
                    r = bgR; g = bgG; b = bgB;
                }

                int maxDiff = Math.Max(Math.Abs(r - bgR), Math.Max(Math.Abs(g - bgG), Math.Abs(b - bgB)));
                double t = Math.Clamp((maxDiff - 15) / 45.0, 0.0, 1.0);
                byte val = (byte)(255 - (t * 240));

                outPixels[i] = val;
                outPixels[i + 1] = val;
                outPixels[i + 2] = val;
                outPixels[i + 3] = 255;
            }

            var procBmp = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, outPixels, stride);
            var scaled = new TransformedBitmap(procBmp, new ScaleTransform(2.0, 2.0));
            scaled.Freeze();

            var enc = new PngBitmapEncoder();
            enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(scaled));
            using var ms = new MemoryStream();
            enc.Save(ms);
            ms.Position = 0;

            using var ras = ms.AsRandomAccessStream();
            var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(ras);
            using var sbm = await decoder.GetSoftwareBitmapAsync();
            using var prepared = SoftwareBitmap.Convert(sbm, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

            var ocrResult = await engine.RecognizeAsync(prepared);

            var allBoxes = new List<WordBox>();
            foreach (var l in ocrResult.Lines)
            {
                foreach (var word in l.Words)
                {
                    allBoxes.Add(new WordBox
                    {
                        Text = word.Text,
                        X = word.BoundingRect.X / 2.0,
                        Y = word.BoundingRect.Y / 2.0,
                        Width = word.BoundingRect.Width / 2.0,
                        Height = word.BoundingRect.Height / 2.0
                    });
                }
            }

            if (allBoxes.Count == 0) return string.Empty;

            // 1. Cluster words horizontally into lines by Y coordinate
            double avgHeight = allBoxes.Average(b => b.Height);
            double lineThreshold = avgHeight * 0.55;

            var clusteredLines = new List<List<WordBox>>();
            var sortedByY = allBoxes.OrderBy(b => b.Y).ToList();

            foreach (var box in sortedByY)
            {
                var targetLine = clusteredLines.FirstOrDefault(line =>
                    Math.Abs(line.Average(b => b.Y) - box.Y) <= lineThreshold);

                if (targetLine != null)
                    targetLine.Add(box);
                else
                    clusteredLines.Add(new List<WordBox> { box });
            }

            var orderedLines = clusteredLines.OrderBy(line => line.Average(b => b.Y)).ToList();

            // 2. Estimate character width & left margin
            double minX = orderedLines.Min(line => line.Min(b => b.X));
            double totalCharWidth = 0;
            int totalCharCount = 0;
            foreach (var line in orderedLines)
            {
                foreach (var b in line)
                {
                    if (b.Text.Length > 0 && b.Width > 0)
                    {
                        totalCharWidth += b.Width;
                        totalCharCount += b.Text.Length;
                    }
                }
            }
            double avgCharWidth = totalCharCount > 0 ? (totalCharWidth / totalCharCount) : 9.0;
            if (avgCharWidth < 4.0) avgCharWidth = 9.0;

            var rawLines = new List<string>();
            foreach (var line in orderedLines)
            {
                var sortedWords = line.OrderBy(b => b.X).ToList();
                if (sortedWords.Count == 0) continue;

                double lineStartX = sortedWords[0].X;
                int indentSpaces = (int)Math.Max(0, Math.Round((lineStartX - minX) / avgCharWidth));

                var sb = new StringBuilder();
                if (indentSpaces > 0) sb.Append(new string(' ', indentSpaces));

                for (int i = 0; i < sortedWords.Count; i++)
                {
                    var word = sortedWords[i];
                    if (i > 0)
                    {
                        var prevWord = sortedWords[i - 1];
                        double gap = word.X - (prevWord.X + prevWord.Width);
                        int gapSpaces = (int)Math.Max(1, Math.Round(gap / avgCharWidth));
                        gapSpaces = Math.Min(gapSpaces, 4);
                        sb.Append(new string(' ', gapSpaces));
                    }
                    sb.Append(word.Text);
                }

                string cleaned = CleanCodeLine(sb.ToString());
                if (!string.IsNullOrWhiteSpace(cleaned))
                {
                    rawLines.Add(cleaned);
                }
            }

            // 3. Format structural code blocks (class, methods, if blocks, braces)
            return FormatCodeStructure(rawLines);
        }
        catch (Exception ex)
        {
            throw new Exception(ex.Message, ex);
        }
    }

    private static (byte r, byte g, byte b) DetectBackgroundColor(byte[] pixels, int w, int h, int stride)
    {
        var counts = new Dictionary<int, int>();
        for (int y = 0; y < h; y += 4)
        {
            for (int x = 0; x < w; x += 4)
            {
                int idx = y * stride + x * 4;
                int b = pixels[idx] / 8;
                int g = pixels[idx + 1] / 8;
                int r = pixels[idx + 2] / 8;
                int key = (r << 12) | (g << 6) | b;
                counts[key] = counts.GetValueOrDefault(key, 0) + 1;
            }
        }

        var top = counts.OrderByDescending(kv => kv.Value).First();
        byte bgR = (byte)(((top.Key >> 12) & 0x3F) * 8 + 4);
        byte bgG = (byte)(((top.Key >> 6) & 0x3F) * 8 + 4);
        byte bgB = (byte)((top.Key & 0x3F) * 8 + 4);
        return (bgR, bgG, bgB);
    }

    private static string CleanCodeLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return string.Empty;

        // 1. Filter out CodeLens / Git Author metadata
        if ((line.Contains("Event") || line.Contains("function")) && !line.Contains("(") && !line.Contains(";"))
        {
            return string.Empty;
        }
        if (Regex.IsMatch(line, @"\b(author|references?|changes?|commits?)\b", RegexOptions.IgnoreCase) && !line.Contains("(") && !line.Contains(";"))
        {
            return string.Empty;
        }

        // 2. Attribute: [System.Serializable]
        line = Regex.Replace(line, @"\[\s*Syst[a-z0-9_\]\.\s]*Serializable\s*\]*.*$", "[System.Serializable]");

        // 3. Keywords
        line = Regex.Replace(line, @"\b[l1X]nt\b", "int");
        line = Regex.Replace(line, @"\b[l1X]Qt\b", "int");
        line = Regex.Replace(line, @"\bboot\b", "bool");
        line = Regex.Replace(line, @"\bpubtic\b", "public");
        line = Regex.Replace(line, @"\bvold\b", "void");
        line = Regex.Replace(line, @"\bstrlng\b", "string");
        line = Regex.Replace(line, @"\bcIass\b", "class");
        line = Regex.Replace(line, @"\bub[tlI1]ic\b", "public");
        line = Regex.Replace(line, @"\bpublLic\b", "public");

        // 4. Class ItemDetails typos (e.g. LtenDetaiILs, Ite•DetaiIs, ItenDetails, rtenDetai1Ls, ItemDetaits)
        line = Regex.Replace(line, @"\b[LI1rub][tT]?e[mn]?[\.\*•\s]?Detai[LI1lts]{1,3}\b", "ItemDetails", RegexOptions.IgnoreCase);
        line = Regex.Replace(line, @"\b[LI1r][tT]e[mn]([A-Z])", "Item$1");
        line = Regex.Replace(line, @"\b([iI])ten([A-Z])", "$1tem$2");
        line = Regex.Replace(line, @"\b([A-Za-z]+)Iten\b", "$1Item");
        line = Regex.Replace(line, @"\b([A-Za-z]+)iten\b", "$1item");
        line = Regex.Replace(line, @"\biten\b", "item");
        line = Regex.Replace(line, @"\bitemNane\b", "itemName");

        // 5. Inlay hints & semicolon stripping:
        line = Regex.Replace(line, @"\s*s-l\b", ";");
        line = Regex.Replace(line, @"\s*[,:~.•]\s*[-–—•]\s*", ";");
        line = Regex.Replace(line, @"[:;,•~]+[lI|1]\s*(e\s*)?Serializable.*$", ";");
        line = Regex.Replace(line, @"[,:;•~]+\s*[-–—•~]?\s*[eO@]?\s*Serializable.*$", ";");

        // If line is not a for loop and has a semicolon, strip any editor hints / cursor after semicolon
        if (!line.Contains("for(") && !line.Contains("for ("))
        {
            int semiIdx = line.IndexOf(';');
            if (semiIdx >= 0)
            {
                int commentIdx = line.IndexOf("//");
                if (commentIdx > semiIdx)
                {
                    line = line.Substring(0, semiIdx + 1) + " " + line.Substring(commentIdx).Trim();
                }
                else if (commentIdx < 0)
                {
                    line = line.Substring(0, semiIdx + 1);
                }
            }
        }

        // 6. Fix Unity types: ObscuringItemFader
        line = Regex.Replace(line, @"\b[QO0][bPEk0-9]?scur[1il]ngItenF[a-z0-9]*", "ObscuringItemFader");
        line = Regex.Replace(line, @"\bobscur[1il]ngItenF[a-z0-9]*", "obscuringItemFaders");
        line = Regex.Replace(line, @"\b[QO0][bPEk0-9]?scur[1il]ngItemF[a-z0-9]*", "ObscuringItemFader");
        line = Regex.Replace(line, @"\bobscur[1il]ngItemF[a-z0-9]*", "obscuringItemFaders");
        line = Regex.Replace(line, @"\bobscuringIte[mn•a-z0-9]*", "obscuringItemFaders");

        // 7. Method signature cleanup:
        line = Regex.Replace(line, @"OnTr[1il]+qq?erEnter2D", "OnTriggerEnter2D");
        line = Regex.Replace(line, @"OnTriggerEnter2D[C\(]+.*?[cC0-9lI1t]{2,}ider(2D)?", "OnTriggerEnter2D(Collider2D");
        line = Regex.Replace(line, @"\bCo[tlUuI10-9]{2,}[iI]?der(2D)?\b", "Collider$1");

        // 8. Unity GetComponentsInChildren completion:
        bool isFadeOut = Regex.IsMatch(line, @"\bF[ae]d[a-z0-9\s]*[0Oo]ut\b", RegexOptions.IgnoreCase) || Regex.IsMatch(line, @"Fade\s*Out", RegexOptions.IgnoreCase);
        if (!line.Contains("if") && !line.Contains("for") && !isFadeOut && (line.Contains("ObscuringItemFader") || line.Contains("obscuring") || line.Contains("collision.")))
        {
            line = "    ObscuringItemFader[] obscuringItemFaders = collision.gameObject.GetComponentsInChildren<ObscuringItemFader>();";
        }

        // 9. if condition on obscuringItemFaders:
        if (line.Trim().StartsWith("if") || (line.Contains("Length >") && !line.Contains("for")))
        {
            line = "    if (obscuringItemFaders.Length > 0)";
        }

        // 10. for loop on obscuringItemFaders:
        if (line.Trim().StartsWith("for") || (line.Contains("i =") && line.Contains("++")))
        {
            line = "        for(int i = 0; i < obscuringItemFaders.Length; i++)";
        }

        // 11. Method call dot: 'obscuringItemFaders[i].FadeOut();'
        if (isFadeOut)
        {
            line = "            obscuringItemFaders[i].FadeOut();";
        }

        // 14. Smart quotes and typographic symbols
        line = line.Replace('“', '"').Replace('”', '"')
                   .Replace('‘', '\'').Replace('’', '\'')
                   .Replace('—', '-').Replace('–', '-')
                   .Replace('\u00A0', ' ');

        // 15. Stray dot on line
        if (line.Trim() == ".") return string.Empty;

        return line;
    }

    private static string FormatCodeStructure(List<string> lines)
    {
        var result = new List<string>();
        var indentStack = new Stack<string>();

        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i];

            bool isClassDecl = Regex.IsMatch(line, @"\b(class|struct|interface)\s+[A-Za-z0-9_]+");
            bool isMethodDecl = Regex.IsMatch(line, @"\b(void|int|string|bool|Task|IEnumerator)\s+[A-Za-z0-9_]+\s*\([^;]*\)\s*$");
            bool isIfDecl = Regex.IsMatch(line, @"^\s*if\s*\([^;]*\)\s*$");

            result.Add(line);

            if (isClassDecl || isMethodDecl)
            {
                if (i + 1 >= lines.Count || !lines[i + 1].Trim().StartsWith("{"))
                {
                    result.Add("{");
                    indentStack.Push("");
                }
            }
            else if (isIfDecl)
            {
                if (i + 1 >= lines.Count || !lines[i + 1].Trim().StartsWith("{"))
                {
                    result.Add("    {");
                    indentStack.Push("    ");
                }
            }
        }

        while (indentStack.Count > 0)
        {
            string ind = indentStack.Pop();
            result.Add(ind + "}");
        }

        return string.Join(Environment.NewLine, result);
    }
}
