using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.BetterDefaultProfilePictures.Configuration;
using SkiaSharp;

namespace Jellyfin.Plugin.BetterDefaultProfilePictures.Drawing;

/// <summary>
/// Generates profile images locally using SkiaSharp.
/// Supports three background styles: Square, Circle, and Pixelated (identicon).
/// </summary>
public sealed class SkiaProfileImageGenerator : IProfileImageGenerator
{
    private const int ImageSize = 256;
    private const int PixelCellCount = 8;
    private const float TwoInitialsFontScaleFactor = 0.38f;
    private const float SingleInitialFontScaleFactor = 0.48f;
    private const float LightenAmount = 0.35f;
    private const float DarkenAmount = 0.3f;

    // A palette of muted background colors suitable for dark-theme UIs.
    private static readonly SKColor[] ColorPalette =
    [
        new SKColor(0x9E, 0x40, 0x40), // Muted Crimson
        new SKColor(0x9E, 0x58, 0x30), // Muted Terra Cotta
        new SKColor(0x9E, 0x78, 0x30), // Muted Ochre
        new SKColor(0x7A, 0x8C, 0x30), // Muted Olive
        new SKColor(0x30, 0x8A, 0x40), // Muted Forest Green
        new SKColor(0x30, 0x8A, 0x6E), // Muted Jade
        new SKColor(0x2E, 0x78, 0x78), // Muted Teal
        new SKColor(0x30, 0x6A, 0x8E), // Muted Steel Blue
        new SKColor(0x30, 0x4E, 0x9E), // Muted Cobalt
        new SKColor(0x4E, 0x30, 0x9E), // Muted Indigo
        new SKColor(0x70, 0x30, 0x9E), // Muted Violet
        new SKColor(0x9E, 0x30, 0x80), // Muted Plum
        new SKColor(0x9E, 0x30, 0x50), // Muted Rose
        new SKColor(0x7A, 0x40, 0x40), // Muted Brick
        new SKColor(0x48, 0x7A, 0x8E), // Muted Cadet Blue
        new SKColor(0x48, 0x90, 0x6E), // Muted Sage
        new SKColor(0x60, 0x48, 0x90), // Muted Slate Purple
        new SKColor(0x90, 0x60, 0x48), // Muted Sienna
        new SKColor(0x40, 0x78, 0x58), // Muted Spruce
        new SKColor(0x78, 0x58, 0x40), // Muted Walnut
    ];

    /// <inheritdoc />
    public Task<Stream> GenerateAsync(
        string displayName,
        Guid userId,
        BackgroundStyle style,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var text = GetInitials(displayName);
        var bgColor = PickColor(userId);

        using var bitmap = style switch
        {
            BackgroundStyle.Square => CreateSquareBitmap(text, bgColor),
            BackgroundStyle.Circle => CreateCircleBitmap(text, bgColor),
            BackgroundStyle.Pixelated => CreatePixelatedBitmap(text, userId, bgColor),
            _ => CreateCircleBitmap(text, bgColor)
        };

        var ms = new MemoryStream();
        bitmap.Encode(ms, SKEncodedImageFormat.Png, 100);
        ms.Position = 0;
        return Task.FromResult<Stream>(ms);
    }

    // ── Bitmap creation ────────────────────────────────────────────────────────

    private static SKBitmap CreateSquareBitmap(string text, SKColor bgColor)
    {
        var bitmap = new SKBitmap(ImageSize, ImageSize);
        using var canvas = new SKCanvas(bitmap);

        canvas.Clear(bgColor);
        DrawCenteredText(canvas, text, SKColors.White, ImageSize);

        return bitmap;
    }

    private static SKBitmap CreateCircleBitmap(string text, SKColor bgColor)
    {
        var bitmap = new SKBitmap(ImageSize, ImageSize);
        using var canvas = new SKCanvas(bitmap);

        canvas.Clear(SKColors.Transparent);

        using var paint = new SKPaint { Color = bgColor, IsAntialias = true };
        canvas.DrawCircle(ImageSize / 2f, ImageSize / 2f, ImageSize / 2f, paint);

        DrawCenteredText(canvas, text, SKColors.White, ImageSize);

        return bitmap;
    }

    private static SKBitmap CreatePixelatedBitmap(string text, Guid userId, SKColor bgColor)
    {
        var bitmap = new SKBitmap(ImageSize, ImageSize);
        using var canvas = new SKCanvas(bitmap);

        canvas.Clear(LightenColor(bgColor, -DarkenAmount)); // darken as bg

        var hash = new byte[16];
        userId.TryWriteBytes(hash);

        var cellSize = ImageSize / PixelCellCount;
        var halfCells = PixelCellCount / 2;
        var accentColor = LightenColor(bgColor, LightenAmount);

        using var cellPaint = new SKPaint { Color = accentColor };
        for (int row = 0; row < PixelCellCount; row++)
        {
            for (int col = 0; col < halfCells; col++)
            {
                var byteIndex = ((row * halfCells) + col) % hash.Length;
                var bit = (hash[byteIndex] >> (col % 8)) & 1;
                if (bit == 1)
                {
                    canvas.DrawRect(
                        SKRect.Create(col * cellSize, row * cellSize, cellSize, cellSize),
                        cellPaint);
                    canvas.DrawRect(
                        SKRect.Create(((PixelCellCount - 1) - col) * cellSize, row * cellSize, cellSize, cellSize),
                        cellPaint);
                }
            }
        }

        DrawCenteredText(canvas, text, SKColors.White, ImageSize);

        return bitmap;
    }

    // ── Text rendering ─────────────────────────────────────────────────────────

    private static void DrawCenteredText(SKCanvas canvas, string text, SKColor color, int imageSize)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var fontSize = text.Length > 1
            ? imageSize * TwoInitialsFontScaleFactor
            : imageSize * SingleInitialFontScaleFactor;

        using var font = new SKFont(SKTypeface.Default, fontSize);
        font.Edging = SKFontEdging.SubpixelAntialias;
        font.Embolden = true;

        using var textPaint = new SKPaint { Color = color, IsAntialias = true };

        // Measure to vertically center
        font.MeasureText(text, out var textBounds, textPaint);

        float x = imageSize / 2f;
        float y = (imageSize / 2f) - textBounds.MidY;

        canvas.DrawText(text, x, y, SKTextAlign.Center, font, textPaint);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static string GetInitials(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return "?";
        }

        var parts = displayName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            return string.Concat(
                char.ToUpperInvariant(parts[0][0]),
                char.ToUpperInvariant(parts[1][0]));
        }

        var name = parts[0];
        return name.Length >= 2
            ? string.Concat(char.ToUpperInvariant(name[0]), char.ToUpperInvariant(name[1]))
            : char.ToUpperInvariant(name[0]).ToString();
    }

    private static SKColor PickColor(Guid userId)
    {
        var hash = Math.Abs(userId.GetHashCode());
        return ColorPalette[hash % ColorPalette.Length];
    }

    private static SKColor LightenColor(SKColor color, float amount)
    {
        return new SKColor(
            (byte)Math.Clamp(color.Red + (int)(255 * amount), 0, 255),
            (byte)Math.Clamp(color.Green + (int)(255 * amount), 0, 255),
            (byte)Math.Clamp(color.Blue + (int)(255 * amount), 0, 255),
            color.Alpha);
    }
}
