// <copyright file="AdminTokenAuthenticationHandler.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AdminApi;

using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MUnique.OpenMU.Persistence.AdminAuth;
using MUnique.OpenMU.Web.AdminPanel.Auth;

/// <summary>
/// Authenticates requests of the admin API by the token in the <see cref="AdminApiDefaults.TokenHeaderName"/> header.
/// </summary>
public class AdminTokenAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly AdminTokenService _tokenService;
    private readonly UserManager<AdminUser> _userManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminTokenAuthenticationHandler"/> class.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="logger">The logger factory.</param>
    /// <param name="encoder">The URL encoder.</param>
    /// <param name="tokenService">The token service.</param>
    /// <param name="userManager">The user manager.</param>
    public AdminTokenAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        AdminTokenService tokenService,
        UserManager<AdminUser> userManager)
        : base(options, logger, encoder)
    {
        this._tokenService = tokenService;
        this._userManager = userManager;
    }

    /// <inheritdoc />
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!this.Request.Headers.TryGetValue(AdminApiDefaults.TokenHeaderName, out var header)
            || header.Count == 0
            || string.IsNullOrWhiteSpace(header[0]))
        {
            return AuthenticateResult.NoResult();
        }

        if (!this._tokenService.TryRead(header[0]!.Trim(), out var payload) || payload is null)
        {
            return AuthenticateResult.Fail("The token is invalid or expired.");
        }

        var user = await this._userManager.FindByIdAsync(payload.UserId.ToString()).ConfigureAwait(false);
        if (user is null
            || user.IsDisabled
            || !string.Equals(user.SecurityStamp, payload.SecurityStamp, StringComparison.Ordinal))
        {
            return AuthenticateResult.Fail("The token is no longer valid.");
        }

        var identity = new ClaimsIdentity(
            AdminLoginService.CreateClaims(user, payload.UsedSecondFactor),
            AdminApiDefaults.AuthenticationScheme,
            ClaimTypes.Name,
            ClaimTypes.Role);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), this.Scheme.Name));
    }

    /// <inheritdoc />
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        this.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        this.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }
}
