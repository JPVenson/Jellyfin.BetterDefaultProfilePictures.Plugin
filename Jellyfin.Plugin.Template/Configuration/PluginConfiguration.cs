using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.BetterDefaultProfilePictures.Configuration;

/// <summary>
/// The background style for locally generated profile images.
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
/// Selects which image generation provider is used.
/// </summary>
public enum GenerationProvider
{
    /// <summary>
    /// Images are generated locally using SkiaSharp.
    /// </summary>
    Local,

    /// <summary>
    /// Images are fetched from the free <c>ui-avatars.com</c> web service.
    /// No API key is required. License: free for public &amp; commercial use.
    /// </summary>
    UiAvatars,

    /// <summary>
    /// Images are fetched from the free <c>api.dicebear.com</c> web service.
    /// License: MIT (API is free to use).
    /// </summary>
    DiceBear
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
        GenerationProvider = GenerationProvider.Local;
        BackgroundStyle = BackgroundStyle.Circle;
        NameFormat = NameFormat.TwoInitials;
        CustomNameTemplate = string.Empty;
        GenerateOnNewUser = true;
        DiceBearStyle = "initials";
    }

    /// <summary>
    /// Gets or sets the provider used to generate profile images.
    /// </summary>
    public GenerationProvider GenerationProvider { get; set; }

    /// <summary>
    /// Gets or sets the background style used when generating profile images locally.
    /// Only used when <see cref="GenerationProvider"/> is <see cref="GenerationProvider.Local"/>.
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

    /// <summary>
    /// Gets or sets the DiceBear avatar style (e.g. <c>initials</c>, <c>bottts</c>,
    /// <c>avataaars</c>, <c>pixel-art</c>). Only used when
    /// <see cref="GenerationProvider"/> is <see cref="GenerationProvider.DiceBear"/>.
    /// Full list at https://www.dicebear.com/styles/.
    /// </summary>
    public string DiceBearStyle { get; set; }
}
