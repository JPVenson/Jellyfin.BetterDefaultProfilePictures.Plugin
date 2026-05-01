using System;
using System.Threading.Tasks;
using Jellyfin.Data.Events.Users;
using Jellyfin.Plugin.BetterDefaultProfilePictures.Drawing;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.BetterDefaultProfilePictures.EventListeners;

/// <summary>
/// Handles the <see cref="UserCreatedEventArgs"/> event and generates a default
/// profile image for newly created users.
/// </summary>
public class UserCreatedEventListener : IEventConsumer<UserCreatedEventArgs>
{
    private readonly IUserManager _userManager;
    private readonly IServerConfigurationManager _serverConfigurationManager;
    private readonly ILogger<UserCreatedEventListener> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserCreatedEventListener"/> class.
    /// </summary>
    /// <param name="userManager">The user manager.</param>
    /// <param name="serverConfigurationManager">The server configuration manager.</param>
    /// <param name="logger">The logger.</param>
    public UserCreatedEventListener(
        IUserManager userManager,
        IServerConfigurationManager serverConfigurationManager,
        ILogger<UserCreatedEventListener> logger)
    {
        _userManager = userManager;
        _serverConfigurationManager = serverConfigurationManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task OnEvent(UserCreatedEventArgs eventArgs)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null || !config.GenerateOnNewUser)
        {
            return;
        }

        try
        {
            using var profileLogger = _logger.BeginScope("UserCreatedEventListener");
            var service = new ProfileImageService(
                _serverConfigurationManager,
                _userManager,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<ProfileImageService>.Instance);

            await service.GenerateAndSaveProfileImageAsync(eventArgs.Argument).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate profile image for new user {UserId}", eventArgs.Argument.Id);
        }
    }
}
