using Jellyfin.Plugin.BetterDefaultProfilePictures.Configuration;

namespace Jellyfin.Plugin.BetterDefaultProfilePictures.Api;

/// <summary>
/// Request body for bulk-generating profile images.
/// </summary>
public class GenerateAllImagesRequest
{
    /// <summary>
    /// Gets or sets an optional background style override to use for all generated images.
    /// When not set the plugin-level configuration value is used.
    /// </summary>
    public BackgroundStyle? BackgroundStyleOverride { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to regenerate images for users that
    /// already have a profile image.
    /// </summary>
    public bool Overwrite { get; set; }
}
