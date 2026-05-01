using System;
using System.Threading.Tasks;
using Jellyfin.Data.Events.Users;
using Jellyfin.Plugin.BetterDefaultProfilePictures.Drawing;
using MediaBrowser.Controller.Events;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.BetterDefaultProfilePictures.EventListeners;

/// <summary>
/// Handles the <see cref="UserCreatedEventArgs"/> event and generates a default
/// profile image for newly created users.
/// </summary>
public class UserCreatedEventListener : IEventConsumer<UserCreatedEventArgs>
{
    private readonly ProfileImageService _profileImageService;
    private readonly ILogger<UserCreatedEventListener> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserCreatedEventListener"/> class.
    /// </summary>
    /// <param name="profileImageService">The profile image service.</param>
    /// <param name="logger">The logger.</param>
    public UserCreatedEventListener(
        ProfileImageService profileImageService,
        ILogger<UserCreatedEventListener> logger)
    {
        _profileImageService = profileImageService;
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
            await _profileImageService.GenerateAndSaveProfileImageAsync(eventArgs.Argument).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate profile image for new user {UserId}", eventArgs.Argument.Id);
        }
    }
}
