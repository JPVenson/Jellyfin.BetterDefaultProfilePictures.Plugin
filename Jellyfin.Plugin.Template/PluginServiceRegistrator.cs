using Jellyfin.Plugin.BetterDefaultProfilePictures.Drawing;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.BetterDefaultProfilePictures;

/// <summary>
/// Registers plugin services into the Jellyfin dependency-injection container.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        // Generators – Singletons because they are stateless.
        serviceCollection.AddSingleton<SkiaProfileImageGenerator>();
        serviceCollection.AddSingleton<UiAvatarsGenerator>();
        serviceCollection.AddSingleton<DiceBearGenerator>();

        // Main service – Scoped so it can safely be injected into scoped services
        // (controllers, event consumers, scheduled tasks).
        serviceCollection.AddScoped<ProfileImageService>();
    }
}
