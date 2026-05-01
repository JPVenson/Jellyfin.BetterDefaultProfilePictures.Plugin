using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.BetterDefaultProfilePictures.Drawing;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.BetterDefaultProfilePictures.Api;

/// <summary>
/// API controller providing admin endpoints for the Better Default Profile Pictures plugin.
/// </summary>
[ApiController]
[Route("Plugins/BetterDefaultProfilePictures")]
[Authorize(Roles = "Administrator")]
public class ProfileImagesController : ControllerBase
{
    private readonly IUserManager _userManager;
    private readonly ProfileImageService _profileImageService;
    private readonly ILogger<ProfileImagesController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileImagesController"/> class.
    /// </summary>
    /// <param name="userManager">The user manager.</param>
    /// <param name="profileImageService">The profile image service.</param>
    /// <param name="logger">The logger.</param>
    public ProfileImagesController(
        IUserManager userManager,
        ProfileImageService profileImageService,
        ILogger<ProfileImagesController> logger)
    {
        _userManager = userManager;
        _profileImageService = profileImageService;
        _logger = logger;
    }

    /// <summary>
    /// Generates or regenerates the profile image for a specific user.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="request">Optional overrides for this generation run.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="NoContentResult"/> on success.</returns>
    [HttpPost("Generate/{userId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GenerateForUserAsync(
        [FromRoute, Required] Guid userId,
        [FromBody] GenerateUserImageRequest? request,
        CancellationToken cancellationToken)
    {
        var user = _userManager.GetUserById(userId);
        if (user is null)
        {
            return NotFound();
        }

        await _profileImageService.GenerateAndSaveProfileImageAsync(
            user,
            request?.DisplayNameOverride,
            request?.BackgroundStyleOverride,
            cancellationToken).ConfigureAwait(false);

        return NoContent();
    }

    /// <summary>
    /// Generates profile images for all users, optionally skipping those that already have one.
    /// </summary>
    /// <param name="request">Optional overrides for this generation run.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An <see cref="OkObjectResult"/> with the list of processed user IDs.</returns>
    [HttpPost("GenerateAll")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<Guid>>> GenerateForAllUsersAsync(
        [FromBody] GenerateAllImagesRequest? request,
        CancellationToken cancellationToken)
    {
        var processed = new List<Guid>();

        foreach (var user in _userManager.Users)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (user.ProfileImage is not null && !(request?.Overwrite ?? false))
            {
                continue;
            }

            try
            {
                await _profileImageService.GenerateAndSaveProfileImageAsync(
                    user,
                    backgroundStyleOverride: request?.BackgroundStyleOverride,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                processed.Add(user.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate profile image for user {UserId}", user.Id);
            }
        }

        return Ok(processed);
    }
}
