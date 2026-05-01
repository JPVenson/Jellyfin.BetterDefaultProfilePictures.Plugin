using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.BetterDefaultProfilePictures.Configuration;

namespace Jellyfin.Plugin.BetterDefaultProfilePictures.Drawing;

/// <summary>
/// Generates a profile image for a given user display name and user ID.
/// </summary>
public interface IProfileImageGenerator
{
    /// <summary>
    /// Generates a PNG profile image and returns it as a stream.
    /// </summary>
    /// <param name="displayName">The text to embed in the image (initials or name).</param>
    /// <param name="userId">The user ID, used for deterministic color or seed selection.</param>
    /// <param name="style">
    /// The background style. Implementations that do not support this parameter may ignore it.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="Stream"/> containing the PNG image bytes (position set to 0).</returns>
    Task<Stream> GenerateAsync(
        string displayName,
        Guid userId,
        BackgroundStyle style,
        CancellationToken cancellationToken);
}
