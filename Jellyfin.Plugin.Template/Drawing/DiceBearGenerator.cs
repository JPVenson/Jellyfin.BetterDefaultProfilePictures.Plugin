using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Jellyfin.Plugin.BetterDefaultProfilePictures.Configuration;

namespace Jellyfin.Plugin.BetterDefaultProfilePictures.Drawing;

/// <summary>
/// Generates profile images by fetching them from the free
/// <c>api.dicebear.com</c> web service (MIT license).
/// See: https://www.dicebear.com/.
/// Popular styles: initials, bottts, avataaars, pixel-art, identicon.
/// Full list at: https://www.dicebear.com/styles/.
/// </summary>
public sealed class DiceBearGenerator : IProfileImageGenerator
{
    private const string BaseUrl = "https://api.dicebear.com/9.x";
    private const string DefaultStyle = "initials";

    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiceBearGenerator"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    public DiceBearGenerator(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    /// <inheritdoc />
    public async Task<Stream> GenerateAsync(
        string displayName,
        Guid userId,
        BackgroundStyle style,
        CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration;
        var avatarStyle = string.IsNullOrWhiteSpace(config?.DiceBearStyle)
            ? DefaultStyle
            : config.DiceBearStyle;

        // DiceBear uses "seed" to deterministically generate the same avatar.
        // We use the user ID as seed to get consistent results.
        var query = HttpUtility.ParseQueryString(string.Empty);
        query["seed"] = userId.ToString("N");
        query["size"] = "256";

        // For the "initials" style we can also pass the name for better results.
        if (string.Equals(avatarStyle, "initials", StringComparison.OrdinalIgnoreCase))
        {
            query["chars"] = GetInitials(displayName);
        }

        var url = $"{BaseUrl}/{avatarStyle}/png?{query}";

        using var client = _httpClientFactory.CreateClient();
        using var response = await client.GetAsync(new Uri(url), cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var ms = new MemoryStream();
        await response.Content.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
        ms.Position = 0;
        return ms;
    }

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
}
