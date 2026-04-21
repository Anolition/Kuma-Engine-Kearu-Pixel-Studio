using System.Diagnostics;
using ProjectSPlus.Editor.Themes;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ProjectSPlus.App.Editor;

public static class ImageImporter
{
    public const int DefaultGeneratedPaletteSize = 32;

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg"
    };

    public static string? ShowImportDialog(string initialDirectory)
    {
        return OperatingSystem.IsWindows()
            ? WindowsNativeFileDialog.ShowOpenFileDialog(initialDirectory, "Image Files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg")
            : ShowLinuxImportDialog(initialDirectory);
    }

    public static ImageImportResult ImportIntoLayer(string filePath, PixelStudioState pixelStudio, PixelStudioLayerState layer)
    {
        if (!IsSupportedFile(filePath))
        {
            throw new InvalidOperationException("Only PNG and JPG images are supported right now.");
        }

        using Image<Rgba32> resizedImage = LoadAndResize(filePath, pixelStudio.CanvasWidth, pixelStudio.CanvasHeight);
        int uniqueSourceColorCount = 0;
        HashSet<ColorKey> seenSourceColors = [];

        for (int y = 0; y < pixelStudio.CanvasHeight; y++)
        {
            for (int x = 0; x < pixelStudio.CanvasWidth; x++)
            {
                int pixelIndex = (y * pixelStudio.CanvasWidth) + x;
                Rgba32 sourcePixel = resizedImage[x, y];
                if (sourcePixel.A <= 8)
                {
                    layer.Pixels[pixelIndex] = 0;
                    continue;
                }

                ColorKey key = new(sourcePixel.R, sourcePixel.G, sourcePixel.B, sourcePixel.A);
                seenSourceColors.Add(key);
                uniqueSourceColorCount = seenSourceColors.Count;
                layer.Pixels[pixelIndex] = PackPixelColor(sourcePixel);
            }
        }

        return new ImageImportResult
        {
            ImportedPixelCount = pixelStudio.CanvasWidth * pixelStudio.CanvasHeight,
            UniqueSourceColorCount = uniqueSourceColorCount,
            PaletteWasModified = false
        };
    }

    public static IReadOnlyList<ThemeColor> GeneratePaletteFromImage(string filePath, int width, int height, int maxColors = DefaultGeneratedPaletteSize)
    {
        if (!IsSupportedFile(filePath))
        {
            throw new InvalidOperationException("Only PNG and JPG images are supported right now.");
        }

        using Image<Rgba32> resizedImage = LoadAndResize(filePath, width, height);
        Dictionary<BucketKey, ColorBucket> buckets = [];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Rgba32 pixel = resizedImage[x, y];
                if (pixel.A <= 8)
                {
                    continue;
                }

                BucketKey key = new((byte)(pixel.R / 32), (byte)(pixel.G / 32), (byte)(pixel.B / 32));
                if (!buckets.TryGetValue(key, out ColorBucket? bucket))
                {
                    bucket = new ColorBucket();
                    buckets[key] = bucket;
                }

                bucket.Count++;
                bucket.SumR += pixel.R;
                bucket.SumG += pixel.G;
                bucket.SumB += pixel.B;
                bucket.SumA += pixel.A;
            }
        }

        List<ThemeColor> colors = buckets.Values
            .OrderByDescending(bucket => bucket.Count)
            .ThenByDescending(bucket => bucket.SumA)
            .Take(Math.Clamp(maxColors, 1, 64))
            .Select(bucket => new ThemeColor(
                bucket.SumR / (255f * bucket.Count),
                bucket.SumG / (255f * bucket.Count),
                bucket.SumB / (255f * bucket.Count),
                bucket.SumA / (255f * bucket.Count)))
            .ToList();

        return colors.Count > 0 ? colors : [new ThemeColor(0.07f, 0.08f, 0.10f), new ThemeColor(0.98f, 0.98f, 0.97f)];
    }

    public static bool IsSupportedFile(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        return SupportedExtensions.Contains(Path.GetExtension(filePath));
    }

    private static Image<Rgba32> LoadAndResize(string filePath, int width, int height)
    {
        using Image<Rgba32> sourceImage = Image.Load<Rgba32>(filePath);
        return sourceImage.Clone(context => context.Resize(new ResizeOptions
        {
            Size = new Size(width, height),
            Sampler = KnownResamplers.NearestNeighbor,
            Mode = ResizeMode.Stretch
        }));
    }

    private static int PackPixelColor(Rgba32 sourcePixel)
    {
        if (sourcePixel.A == 0)
        {
            return 0;
        }

        return (sourcePixel.A << 24)
            | (sourcePixel.R << 16)
            | (sourcePixel.G << 8)
            | sourcePixel.B;
    }

    private static string? ShowLinuxImportDialog(string initialDirectory)
    {
        string directory = Directory.Exists(initialDirectory)
            ? initialDirectory
            : Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

        if (TryRunLinuxDialog("zenity", $"--file-selection --title=\"Import Image\" --filename=\"{directory}{Path.DirectorySeparatorChar}\" --file-filter=\"Images | *.png *.jpg *.jpeg\"", out string? zenityPath))
        {
            return zenityPath;
        }

        if (TryRunLinuxDialog("kdialog", $"--getopenfilename \"{directory}\" \"Images (*.png *.jpg *.jpeg)\"", out string? kdialogPath))
        {
            return kdialogPath;
        }

        return null;
    }

    private static bool TryRunLinuxDialog(string command, string arguments, out string? selectedPath)
    {
        selectedPath = RunDialogProcess(command, arguments);
        return !string.IsNullOrWhiteSpace(selectedPath);
    }

    private static string? RunDialogProcess(string fileName, string arguments)
    {
        try
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process? process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            string output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            return process.ExitCode == 0 && IsSupportedFile(output) ? output : null;
        }
        catch
        {
            return null;
        }
    }

    private sealed class ColorBucket
    {
        public int Count { get; set; }

        public int SumR { get; set; }

        public int SumG { get; set; }

        public int SumB { get; set; }

        public int SumA { get; set; }
    }

    private readonly record struct ColorKey(byte R, byte G, byte B, byte A);

    private readonly record struct BucketKey(byte R, byte G, byte B);
}

public sealed class ImageImportResult
{
    public required int ImportedPixelCount { get; init; }

    public required int UniqueSourceColorCount { get; init; }

    public required bool PaletteWasModified { get; init; }
}
