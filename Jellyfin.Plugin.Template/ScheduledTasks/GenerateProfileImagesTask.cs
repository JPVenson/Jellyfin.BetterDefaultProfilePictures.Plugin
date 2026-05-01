using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.BetterDefaultProfilePictures.Drawing;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.BetterDefaultProfilePictures.ScheduledTasks;

/// <summary>
/// A scheduled task that generates default profile images for all users that do
/// not yet have a custom profile image set.
/// </summary>
public class GenerateProfileImagesTask : IScheduledTask
{
    private readonly IUserManager _userManager;
    private readonly IServerConfigurationManager _serverConfigurationManager;
    private readonly ILogger<GenerateProfileImagesTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateProfileImagesTask"/> class.
    /// </summary>
    /// <param name="userManager">The user manager.</param>
    /// <param name="serverConfigurationManager">The server configuration manager.</param>
    /// <param name="logger">The logger.</param>
    public GenerateProfileImagesTask(
        IUserManager userManager,
        IServerConfigurationManager serverConfigurationManager,
        ILogger<GenerateProfileImagesTask> logger)
    {
        _userManager = userManager;
        _serverConfigurationManager = serverConfigurationManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Generate Default Profile Pictures";

    /// <inheritdoc />
    public string Key => "BetterDefaultProfilePictures_GenerateAll";

    /// <inheritdoc />
    public string Description => "Generates a default profile picture for every user account that does not have one.";

    /// <inheritdoc />
    public string Category => "Better Default Profile Pictures";

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var service = new ProfileImageService(
            _serverConfigurationManager,
            _userManager,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ProfileImageService>.Instance);

        var users = _userManager.Users;
        var userList = new List<Jellyfin.Data.Entities.User>(users);
        var total = userList.Count;
        var processed = 0;

        foreach (var user in userList)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (user.ProfileImage is not null)
            {
                _logger.LogDebug("Skipping user {Username} – already has a profile image", user.Username);
                processed++;
                progress.Report(100.0 * processed / total);
                continue;
            }

            try
            {
                await service.GenerateAndSaveProfileImageAsync(user, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate profile image for user {UserId}", user.Id);
            }

            processed++;
            progress.Report(100.0 * processed / total);
        }
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        // Run once on startup
        return
        [
            new TaskTriggerInfo { Type = TaskTriggerInfo.TriggerStartup }
        ];
    }
}
