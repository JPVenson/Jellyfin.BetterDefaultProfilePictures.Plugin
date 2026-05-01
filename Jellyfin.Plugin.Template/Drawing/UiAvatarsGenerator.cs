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
/// <c>ui-avatars.com</c> web service. No API key is required.
/// The service is free for public and commercial use.
/// See: https://ui-avatars.com/.
/// </summary>
public sealed class UiAvatarsGenerator : IProfileImageGenerator
{
    private const string BaseUrl = "https://ui-avatars.com/api/";

    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="UiAvatarsGenerator"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    public UiAvatarsGenerator(IHttpClientFactory httpClientFactory)
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
        // Pick a background color deterministically from the user ID.
        var hash = Math.Abs(userId.GetHashCode());
        var hue = (hash * 137) % 360; // golden-angle distribution
        var bgHex = HslToHex(hue, 0.45f, 0.40f);

        var query = HttpUtility.ParseQueryString(string.Empty);
        query["name"] = displayName;
        query["background"] = bgHex;
        query["color"] = "ffffff";
        query["size"] = "256";
        query["bold"] = "true";
        query["format"] = "png";

        // rounded = true gives a circle-like result; square keeps it flat.
        if (style == BackgroundStyle.Circle)
        {
            query["rounded"] = "true";
        }

        var url = BaseUrl + "?" + query;

        using var client = _httpClientFactory.CreateClient();
        using var response = await client.GetAsync(new Uri(url), cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var ms = new MemoryStream();
        await response.Content.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
        ms.Position = 0;
        return ms;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Converts an HSL color to a 6-character RGB hex string (no leading #).
    /// </summary>
    private static string HslToHex(int hue, float saturation, float lightness)
    {
        float h = hue / 360f;
        float r, g, b;

        if (saturation == 0f)
        {
            r = g = b = lightness;
        }
        else
        {
            float q = lightness < 0.5f
                ? lightness * (1f + saturation)
                : (lightness + saturation) - (lightness * saturation);
            float p = (2f * lightness) - q;
            r = HueToRgb(p, q, h + (1f / 3f));
            g = HueToRgb(p, q, h);
            b = HueToRgb(p, q, h - (1f / 3f));
        }

        return string.Concat(
            ((int)(r * 255)).ToString("X2", System.Globalization.CultureInfo.InvariantCulture),
            ((int)(g * 255)).ToString("X2", System.Globalization.CultureInfo.InvariantCulture),
            ((int)(b * 255)).ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
    }

    private static float HueToRgb(float p, float q, float t)
    {
        if (t < 0f)
        {
            t += 1f;
        }

        if (t > 1f)
        {
            t -= 1f;
        }

        if (t < 1f / 6f)
        {
            return p + ((q - p) * 6f * t);
        }

        if (t < 1f / 2f)
        {
            return q;
        }

        if (t < 2f / 3f)
        {
            return p + ((q - p) * ((2f / 3f) - t) * 6f);
        }

        return p;
    }
}
