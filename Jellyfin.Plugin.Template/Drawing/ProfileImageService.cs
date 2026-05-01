using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.BetterDefaultProfilePictures.Configuration;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;
using IOPath = System.IO.Path;

namespace Jellyfin.Plugin.BetterDefaultProfilePictures.Drawing;

/// <summary>
/// Orchestrates profile-image generation and persistence for Jellyfin users.
/// Delegates actual image creation to the appropriate <see cref="IProfileImageGenerator"/>
/// based on the current plugin configuration.
/// </summary>
public class ProfileImageService
{
    private readonly IServerConfigurationManager _serverConfigurationManager;
    private readonly IUserManager _userManager;
    private readonly ILogger<ProfileImageService> _logger;
    private readonly SkiaProfileImageGenerator _localGenerator;
    private readonly UiAvatarsGenerator _uiAvatarsGenerator;
    private readonly DiceBearGenerator _diceBearGenerator;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileImageService"/> class.
    /// </summary>
    /// <param name="serverConfigurationManager">The server configuration manager.</param>
    /// <param name="userManager">The user manager.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="localGenerator">The SkiaSharp local image generator.</param>
    /// <param name="uiAvatarsGenerator">The UI Avatars web-service generator.</param>
    /// <param name="diceBearGenerator">The DiceBear web-service generator.</param>
    public ProfileImageService(
        IServerConfigurationManager serverConfigurationManager,
        IUserManager userManager,
        ILogger<ProfileImageService> logger,
        SkiaProfileImageGenerator localGenerator,
        UiAvatarsGenerator uiAvatarsGenerator,
        DiceBearGenerator diceBearGenerator)
    {
        _serverConfigurationManager = serverConfigurationManager;
        _userManager = userManager;
        _logger = logger;
        _localGenerator = localGenerator;
        _uiAvatarsGenerator = uiAvatarsGenerator;
        _diceBearGenerator = diceBearGenerator;
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
        User user,
        string? displayNameOverride = null,
        BackgroundStyle? backgroundStyleOverride = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogDebug("Generating profile image for user {UserId} ({Username})", user.Id, user.Username);

        var config = Plugin.Instance?.Configuration;
        var style = backgroundStyleOverride ?? config?.BackgroundStyle ?? BackgroundStyle.Circle;
        var displayName = ResolveDisplayName(user.Username, displayNameOverride, config);
        var provider = config?.GenerationProvider ?? GenerationProvider.Local;

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

        IProfileImageGenerator generator = provider switch
        {
            GenerationProvider.UiAvatars => _uiAvatarsGenerator,
            GenerationProvider.DiceBear => _diceBearGenerator,
            _ => _localGenerator
        };

        using var imageStream = await generator.GenerateAsync(displayName, user.Id, style, cancellationToken)
            .ConfigureAwait(false);

        var fileStream = new FileStream(imagePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await using (fileStream.ConfigureAwait(false))
        {
            await imageStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
        }

        user.ProfileImage = new Jellyfin.Database.Implementations.Entities.ImageInfo(imagePath);
        await _userManager.UpdateUserAsync(user).ConfigureAwait(false);

        _logger.LogInformation(
            "Saved generated profile image for user {UserId} ({Username}) to {Path} [provider={Provider}]",
            user.Id,
            user.Username,
            imagePath,
            provider);
    }

    // ── Name resolution ────────────────────────────────────────────────────────

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
            NameFormat.FullFirstName => GetFirstWord(username),
            _ => username
        };
    }

    private static string GetFirstWord(string value)
    {
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0] : "?";
    }
}
