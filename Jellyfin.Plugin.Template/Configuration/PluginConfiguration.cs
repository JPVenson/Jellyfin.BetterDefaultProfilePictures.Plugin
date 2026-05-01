using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.BetterDefaultProfilePictures.Configuration;

/// <summary>
/// The background style for generated profile images.
/// </summary>
public enum BackgroundStyle
{
    /// <summary>
    /// A solid square background with initials centered on it.
    /// </summary>
    Square,

    /// <summary>
    /// A solid circle background with initials centered on it.
    /// </summary>
    Circle,

    /// <summary>
    /// A pixelated / identicon-style pattern background with initials overlaid.
    /// </summary>
    Pixelated
}

/// <summary>
/// Controls how the display text on the profile image is derived.
/// </summary>
public enum NameFormat
{
    /// <summary>
    /// Show two initials: first letter of first word + first letter of second word (or second character of a single word).
    /// </summary>
    TwoInitials,

    /// <summary>
    /// Show only the first initial.
    /// </summary>
    FirstInitial,

    /// <summary>
    /// Show the full first word of the display name.
    /// </summary>
    FullFirstName
}

/// <summary>
/// Plugin configuration for Better Default Profile Pictures.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        BackgroundStyle = BackgroundStyle.Circle;
        NameFormat = NameFormat.TwoInitials;
        CustomNameTemplate = string.Empty;
        GenerateOnNewUser = true;
    }

    /// <summary>
    /// Gets or sets the background style used when generating profile images.
    /// </summary>
    public BackgroundStyle BackgroundStyle { get; set; }

    /// <summary>
    /// Gets or sets the format used to derive the text displayed on the profile image.
    /// When <see cref="NameFormat"/> is set this controls the auto-derived text.
    /// </summary>
    public NameFormat NameFormat { get; set; }

    /// <summary>
    /// Gets or sets an optional custom display-name template that overrides the
    /// <see cref="NameFormat"/> selection. Use <c>{0}</c> as a placeholder for the
    /// username.  Leave empty to use the automatic format.
    /// </summary>
    public string CustomNameTemplate { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a profile image should be
    /// automatically generated when a new user account is created.
    /// </summary>
    public bool GenerateOnNewUser { get; set; }
}
