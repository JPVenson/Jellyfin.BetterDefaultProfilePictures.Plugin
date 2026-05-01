using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.BetterDefaultProfilePictures.Configuration;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using IOPath = System.IO.Path;
using JellyfinImageInfo = Jellyfin.Data.Entities.ImageInfo;

namespace Jellyfin.Plugin.BetterDefaultProfilePictures.Drawing;

/// <summary>
/// Generates and saves default profile images for Jellyfin users.
/// </summary>
public class ProfileImageService
{
    private const int ImageSize = 256;
    private const int PixelCellCount = 8;

    // A palette of muted background colors suitable for dark-theme UIs.
    private static readonly Color[] ColorPalette =
    [
        Color.FromRgb(0x9E, 0x40, 0x40), // Muted Crimson
        Color.FromRgb(0x9E, 0x58, 0x30), // Muted Terra Cotta
        Color.FromRgb(0x9E, 0x78, 0x30), // Muted Ochre
        Color.FromRgb(0x7A, 0x8C, 0x30), // Muted Olive
        Color.FromRgb(0x30, 0x8A, 0x40), // Muted Forest Green
        Color.FromRgb(0x30, 0x8A, 0x6E), // Muted Jade
        Color.FromRgb(0x2E, 0x78, 0x78), // Muted Teal
        Color.FromRgb(0x30, 0x6A, 0x8E), // Muted Steel Blue
        Color.FromRgb(0x30, 0x4E, 0x9E), // Muted Cobalt
        Color.FromRgb(0x4E, 0x30, 0x9E), // Muted Indigo
        Color.FromRgb(0x70, 0x30, 0x9E), // Muted Violet
        Color.FromRgb(0x9E, 0x30, 0x80), // Muted Plum
        Color.FromRgb(0x9E, 0x30, 0x50), // Muted Rose
        Color.FromRgb(0x7A, 0x40, 0x40), // Muted Brick
        Color.FromRgb(0x48, 0x7A, 0x8E), // Muted Cadet Blue
        Color.FromRgb(0x48, 0x90, 0x6E), // Muted Sage
        Color.FromRgb(0x60, 0x48, 0x90), // Muted Slate Purple
        Color.FromRgb(0x90, 0x60, 0x48), // Muted Sienna
        Color.FromRgb(0x40, 0x78, 0x58), // Muted Spruce
        Color.FromRgb(0x78, 0x58, 0x40), // Muted Walnut
    ];

    private readonly IServerConfigurationManager _serverConfigurationManager;
    private readonly IUserManager _userManager;
    private readonly ILogger<ProfileImageService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileImageService"/> class.
    /// </summary>
    /// <param name="serverConfigurationManager">The server configuration manager.</param>
    /// <param name="userManager">The user manager.</param>
    /// <param name="logger">The logger.</param>
    public ProfileImageService(
        IServerConfigurationManager serverConfigurationManager,
        IUserManager userManager,
        ILogger<ProfileImageService> logger)
    {
        _serverConfigurationManager = serverConfigurationManager;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// Generates and saves a profile image for the given user, optionally overriding the
    /// plugin-level settings with the provided parameters.
    /// </summary>
    /// <param name="user">The user to generate the image for.</param>
    /// <param name="displayNameOverride">Override for the display name (uses username if null).</param>
    /// <param name="backgroundStyleOverride">Override for the background style (uses plugin config if null).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task GenerateAndSaveProfileImageAsync(
        Jellyfin.Data.Entities.User user,
        string? displayNameOverride = null,
        BackgroundStyle? backgroundStyleOverride = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogDebug("Generating profile image for user {UserId} ({Username})", user.Id, user.Username);

        var config = Plugin.Instance?.Configuration;
        var style = backgroundStyleOverride ?? config?.BackgroundStyle ?? BackgroundStyle.Circle;
        var displayName = ResolveDisplayName(user.Username, displayNameOverride, config);

        if (user.ProfileImage is not null)
        {
            // Only delete a previously generated profile image that lives inside the
            // user configuration directory to guard against path injection.
            var userConfigRoot = _serverConfigurationManager.ApplicationPaths.UserConfigurationDirectoryPath;
            var fullOldPath = IOPath.GetFullPath(user.ProfileImage.Path);
            if (fullOldPath.StartsWith(IOPath.GetFullPath(userConfigRoot), StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    // CA3003 is suppressed: the path is verified to be within the user configuration
                    // directory immediately above, preventing any path traversal attack.
#pragma warning disable CA3003
                    File.Delete(fullOldPath);
#pragma warning restore CA3003
                    _logger.LogDebug("Deleted previous profile image at {Path}", fullOldPath);
                }
                catch (IOException ex)
                {
                    _logger.LogError(ex, "Failed to delete existing profile image at {Path}", fullOldPath);
                }
            }

            await _userManager.ClearProfileImageAsync(user).ConfigureAwait(false);
        }

        var userDataPath = IOPath.Combine(
            _serverConfigurationManager.ApplicationPaths.UserConfigurationDirectoryPath,
            user.Username);

        Directory.CreateDirectory(userDataPath);
        var imagePath = IOPath.Combine(userDataPath, "profile.png");

        using var imageStream = GenerateProfileImage(displayName, user.Id, style);
        var fileStream = new FileStream(imagePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await using (fileStream.ConfigureAwait(false))
        {
            await imageStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
        }

        user.ProfileImage = new JellyfinImageInfo(imagePath);
        await _userManager.UpdateUserAsync(user).ConfigureAwait(false);

        _logger.LogInformation(
            "Saved generated profile image for user {UserId} ({Username}) to {Path}",
            user.Id,
            user.Username,
            imagePath);
    }

    /// <summary>
    /// Generates a PNG profile image stream for the given display name and user ID.
    /// </summary>
    /// <param name="displayName">The display name to derive initials from.</param>
    /// <param name="userId">The user ID used to deterministically pick a color.</param>
    /// <param name="style">The background style to use.</param>
    /// <returns>A <see cref="MemoryStream"/> containing the PNG image data.</returns>
    public MemoryStream GenerateProfileImage(string displayName, Guid userId, BackgroundStyle style)
    {
        var text = GetInitials(displayName);
        var bgColor = PickColor(userId);

        using var image = style switch
        {
            BackgroundStyle.Square => CreateSquareImage(text, bgColor),
            BackgroundStyle.Circle => CreateCircleImage(text, bgColor),
            BackgroundStyle.Pixelated => CreatePixelatedImage(text, userId, bgColor),
            _ => CreateCircleImage(text, bgColor)
        };

        var ms = new MemoryStream();
        image.SaveAsPng(ms);
        ms.Position = 0;
        return ms;
    }

    private static Image<Rgba32> CreateSquareImage(string text, Color bgColor)
    {
        var image = new Image<Rgba32>(ImageSize, ImageSize);
        image.Mutate(ctx =>
        {
            ctx.Fill(bgColor);
            DrawCenteredText(ctx, text, Color.White, ImageSize);
        });
        return image;
    }

    private static Image<Rgba32> CreateCircleImage(string text, Color bgColor)
    {
        var image = new Image<Rgba32>(ImageSize, ImageSize);
        image.Mutate(ctx =>
        {
            ctx.Fill(Color.Transparent);
            ctx.Fill(bgColor, new SixLabors.ImageSharp.Drawing.EllipsePolygon(ImageSize / 2f, ImageSize / 2f, ImageSize / 2f));
            DrawCenteredText(ctx, text, Color.White, ImageSize);
        });
        return image;
    }

    private static Image<Rgba32> CreatePixelatedImage(string text, Guid userId, Color bgColor)
    {
        var image = new Image<Rgba32>(ImageSize, ImageSize);

        // Build deterministic pixel pattern from userId bytes
        var hash = new byte[16];
        userId.TryWriteBytes(hash);

        var cellSize = ImageSize / PixelCellCount;
        var halfCells = PixelCellCount / 2;
        var accentColor = LightenColor(bgColor, 0.35f);

        image.Mutate(ctx =>
        {
            ctx.Fill(DarkenColor(bgColor, 0.3f));

            // Draw symmetric pixel pattern (left half mirrored to right half)
            for (int row = 0; row < PixelCellCount; row++)
            {
                for (int col = 0; col < halfCells; col++)
                {
                    var byteIndex = ((row * halfCells) + col) % hash.Length;
                    var bit = (hash[byteIndex] >> (col % 8)) & 1;
                    if (bit == 1)
                    {
                        ctx.Fill(accentColor, new SixLabors.ImageSharp.Rectangle(col * cellSize, row * cellSize, cellSize, cellSize));
                        ctx.Fill(accentColor, new SixLabors.ImageSharp.Rectangle(((PixelCellCount - 1) - col) * cellSize, row * cellSize, cellSize, cellSize));
                    }
                }
            }

            DrawCenteredText(ctx, text, Color.White, ImageSize);
        });

        return image;
    }

    private static void DrawCenteredText(IImageProcessingContext ctx, string text, Color textColor, int imageSize)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        // Try to find a usable font family from the system
        FontFamily? fontFamily = null;
        foreach (var name in new[] { "DejaVu Sans", "Liberation Sans", "Arial", "Helvetica", "FreeSans", "Noto Sans" })
        {
            if (SystemFonts.TryGet(name, out var found))
            {
                fontFamily = found;
                break;
            }
        }

        if (fontFamily is null)
        {
            fontFamily = SystemFonts.Families.FirstOrDefault();
        }

        if (fontFamily is null)
        {
            // No system font available – skip text rendering
            return;
        }

        var fontSize = text.Length > 1 ? (imageSize * 0.38f) : (imageSize * 0.48f);
        var font = fontFamily.Value.CreateFont(fontSize, FontStyle.Bold);

        var textOptions = new RichTextOptions(font)
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Origin = new System.Numerics.Vector2(imageSize / 2f, imageSize / 2f)
        };

        ctx.DrawText(textOptions, text, textColor);
    }

    /// <summary>
    /// Extracts the display text from a display name.
    /// </summary>
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
                char.ToUpperInvariant(parts[0][0]).ToString(),
                char.ToUpperInvariant(parts[1][0]).ToString());
        }

        var name = parts[0];
        return name.Length >= 2
            ? string.Concat(char.ToUpperInvariant(name[0]).ToString(), char.ToUpperInvariant(name[1]).ToString())
            : char.ToUpperInvariant(name[0]).ToString();
    }

    /// <summary>
    /// Resolves the effective display name for a user.
    /// </summary>
    private static string ResolveDisplayName(string username, string? overrideValue, PluginConfiguration? config)
    {
        if (!string.IsNullOrWhiteSpace(overrideValue))
        {
            return overrideValue;
        }

        if (config is not null && !string.IsNullOrWhiteSpace(config.CustomNameTemplate))
        {
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, config.CustomNameTemplate, username);
        }

        if (config is null)
        {
            return username;
        }

        return config.NameFormat switch
        {
            NameFormat.FirstInitial => username.Length > 0 ? char.ToUpperInvariant(username[0]).ToString() : "?",
            NameFormat.FullFirstName => username.Trim().Split(' ')[0],
            _ => username
        };
    }

    /// <summary>
    /// Deterministically picks a background color based on the user's ID.
    /// </summary>
    private static Color PickColor(Guid userId)
    {
        var hash = Math.Abs(userId.GetHashCode());
        return ColorPalette[hash % ColorPalette.Length];
    }

    private static Color LightenColor(Color color, float amount)
    {
        var pixel = color.ToPixel<Rgba32>();
        return Color.FromRgb(
            (byte)Math.Min(255, pixel.R + (int)(255 * amount)),
            (byte)Math.Min(255, pixel.G + (int)(255 * amount)),
            (byte)Math.Min(255, pixel.B + (int)(255 * amount)));
    }

    private static Color DarkenColor(Color color, float amount)
    {
        var pixel = color.ToPixel<Rgba32>();
        return Color.FromRgb(
            (byte)Math.Max(0, pixel.R - (int)(255 * amount)),
            (byte)Math.Max(0, pixel.G - (int)(255 * amount)),
            (byte)Math.Max(0, pixel.B - (int)(255 * amount)));
    }
}
