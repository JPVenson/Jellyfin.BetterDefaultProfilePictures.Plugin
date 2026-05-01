using Jellyfin.Plugin.BetterDefaultProfilePictures.Configuration;

namespace Jellyfin.Plugin.BetterDefaultProfilePictures.Api;

/// <summary>
/// Request body for generating a profile image for a specific user.
/// </summary>
public class GenerateUserImageRequest
{
    /// <summary>
    /// Gets or sets an optional display name override. When set the image text
    /// is derived from this value instead of the username.
    /// </summary>
    public string? DisplayNameOverride { get; set; }

    /// <summary>
    /// Gets or sets an optional background style override for this request.
    /// When not set the plugin-level configuration value is used.
    /// </summary>
    public BackgroundStyle? BackgroundStyleOverride { get; set; }
}
