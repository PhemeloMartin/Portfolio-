using System.Text;
using System.Text.RegularExpressions;
using SkiaSharp;

namespace Portfolio.Web.Services;

/// <summary>An upload was rejected. The message is safe to show to the admin.</summary>
public sealed class InvalidUploadException(string message) : Exception(message);

public sealed record ProcessedImage(byte[] Data, int Width, int Height, string ContentType);

public static class UploadLimits
{
    public const long ImageMaxBytes = 8 * 1024 * 1024;
    public const long PdfMaxBytes = 10 * 1024 * 1024;
    public const long RequestMaxBytes = 12 * 1024 * 1024;
}

/// <summary>Checks a file's real type from its first bytes instead of trusting its name or the browser.</summary>
public static class FileSignatures
{
    public static bool IsJpeg(ReadOnlySpan<byte> d) => d.Length > 3 && d[0] == 0xFF && d[1] == 0xD8 && d[2] == 0xFF;

    public static bool IsPng(ReadOnlySpan<byte> d) =>
        d.Length > 8 && d[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

    public static bool IsWebP(ReadOnlySpan<byte> d) =>
        d.Length > 12 && d[..4].SequenceEqual("RIFF"u8) && d[8..12].SequenceEqual("WEBP"u8);

    public static bool IsPdf(ReadOnlySpan<byte> d) => d.Length > 5 && d[..5].SequenceEqual("%PDF-"u8);
}

public static class UploadReader
{
    public static async Task<byte[]> ReadAsync(IFormFile? file, long maxBytes, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            throw new InvalidUploadException("Choose a file to upload.");
        if (file.Length > maxBytes)
            throw new InvalidUploadException($"That file is too large. The limit is {maxBytes / (1024 * 1024)} MB.");

        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream((int)file.Length);
        await stream.CopyToAsync(buffer, ct);
        return buffer.ToArray();
    }
}

public static partial class PdfValidator
{
    // PDFs that can run scripts, launch programs or carry hidden files have no place on this site.
    [GeneratedRegex(@"/(JavaScript|JS|Launch|EmbeddedFile|RichMedia|OpenAction\s*<<\s*/S\s*/JavaScript)\b")]
    private static partial Regex ActiveContent();

    public static void Validate(byte[] data)
    {
        if (!FileSignatures.IsPdf(data))
            throw new InvalidUploadException("That file isn't a PDF.");
        if (ActiveContent().IsMatch(Encoding.Latin1.GetString(data)))
            throw new InvalidUploadException("That PDF contains scripts or embedded files, so it can't be published. Export a plain PDF and try again.");
    }
}

/// <summary>
/// Turns an uploaded photo into a clean JPEG: checks the real format, guards against huge "decompression bomb"
/// images, applies the camera rotation, scales it down, and re-encodes it, which drops all metadata (including GPS).
/// </summary>
public static class ImageProcessor
{
    private const long MaxSourcePixels = 50_000_000;

    public static ProcessedImage ToSafeJpeg(byte[] input, int maxEdge = 1600, int quality = 85)
    {
        if (!FileSignatures.IsJpeg(input) && !FileSignatures.IsPng(input) && !FileSignatures.IsWebP(input))
            throw new InvalidUploadException("Upload a JPEG, PNG or WebP image.");

        using var data = SKData.CreateCopy(input);
        using var codec = SKCodec.Create(data) ?? throw new InvalidUploadException("That image couldn't be read.");

        var info = codec.Info;
        if (info.Width <= 0 || info.Height <= 0 || (long)info.Width * info.Height > MaxSourcePixels)
            throw new InvalidUploadException("That image is too large. Use one under 50 megapixels.");

        using var decoded = SKBitmap.Decode(codec) ?? throw new InvalidUploadException("That image couldn't be read.");
        using var oriented = Orient(decoded, codec.EncodedOrigin);

        var scale = Math.Min(1.0, (double)maxEdge / Math.Max(oriented.Width, oriented.Height));
        var width = Math.Max(1, (int)Math.Round(oriented.Width * scale));
        var height = Math.Max(1, (int)Math.Round(oriented.Height * scale));

        using var resized = scale < 1.0
            ? oriented.Resize(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque),
                              new SKSamplingOptions(SKCubicResampler.Mitchell))
              ?? throw new InvalidUploadException("That image couldn't be resized.")
            : oriented.Copy();

        using var image = SKImage.FromBitmap(resized);
        using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, quality)
                            ?? throw new InvalidUploadException("That image couldn't be saved.");

        return new ProcessedImage(encoded.ToArray(), width, height, "image/jpeg");
    }

    /// <summary>Draws the image upright onto an opaque white canvas (so transparent PNGs don't turn black).</summary>
    private static SKBitmap Orient(SKBitmap src, SKEncodedOrigin origin)
    {
        var swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
                          or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        int w = swap ? src.Height : src.Width, h = swap ? src.Width : src.Height;

        var dst = new SKBitmap(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Opaque));
        using var canvas = new SKCanvas(dst);
        canvas.Clear(SKColors.White);

        switch (origin)
        {
            case SKEncodedOrigin.TopRight: canvas.Translate(w, 0); canvas.Scale(-1, 1); break;
            case SKEncodedOrigin.BottomRight: canvas.Translate(w, h); canvas.RotateDegrees(180); break;
            case SKEncodedOrigin.BottomLeft: canvas.Translate(0, h); canvas.Scale(1, -1); break;
            case SKEncodedOrigin.LeftTop: canvas.RotateDegrees(90); canvas.Scale(1, -1); break;
            case SKEncodedOrigin.RightTop: canvas.Translate(w, 0); canvas.RotateDegrees(90); break;
            case SKEncodedOrigin.RightBottom: canvas.Translate(w, h); canvas.RotateDegrees(270); canvas.Scale(1, -1); break;
            case SKEncodedOrigin.LeftBottom: canvas.Translate(0, h); canvas.RotateDegrees(270); break;
        }

        using var source = SKImage.FromBitmap(src);
        canvas.DrawImage(source, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest));
        canvas.Flush();
        return dst;
    }
}
