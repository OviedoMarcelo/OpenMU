// <copyright file="AuthController.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AdminApi;

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MUnique.OpenMU.Persistence.AdminAuth;
using MUnique.OpenMU.Web.AdminPanel.Auth;

/// <summary>
/// The login of the admin API.
/// </summary>
/// <remarks>
/// The checks are the same as in the login page of the admin panel, see <see cref="AdminLoginService"/>.
/// The API is stateless, so the password is sent again together with the second factor.
/// </remarks>
[ApiController]
[Route(AdminApiDefaults.RoutePrefix + "/auth")]
[Authorize(AuthenticationSchemes = AdminApiDefaults.AuthenticationScheme, Policy = AdminPolicies.Viewer)]
public class AuthController : ControllerBase
{
    private readonly AdminLoginService _loginService;
    private readonly AdminTokenService _tokenService;
    private readonly IOptions<AdminPanelAuthOptions> _authOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthController"/> class.
    /// </summary>
    /// <param name="loginService">The login service.</param>
    /// <param name="tokenService">The token service.</param>
    /// <param name="authOptions">The authentication options.</param>
    public AuthController(AdminLoginService loginService, AdminTokenService tokenService, IOptions<AdminPanelAuthOptions> authOptions)
    {
        this._loginService = loginService;
        this._tokenService = tokenService;
        this._authOptions = authOptions;
    }

    /// <summary>
    /// Checks the credentials and issues a token.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>The result of the login attempt.</returns>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> LoginAsync([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.LoginName) || string.IsNullOrEmpty(request.Password))
        {
            return this.Unauthorized(new LoginResponse(AdminApiLoginStatus.Failed));
        }

        var result = await this._loginService.CheckPasswordAsync(request.LoginName.Trim(), request.Password, false).ConfigureAwait(false);
        if (result.Status == AdminLoginStatus.TwoFactorRequired)
        {
            if (string.IsNullOrWhiteSpace(request.Code))
            {
                return this.Ok(new LoginResponse(AdminApiLoginStatus.TwoFactorRequired));
            }

            result = await this._loginService.CheckTwoFactorAsync(request.Code, request.IsRecoveryCode).ConfigureAwait(false);
        }

        switch (result.Status)
        {
            case AdminLoginStatus.LockedOut:
                return this.StatusCode(StatusCodes.Status423Locked, new LoginResponse(AdminApiLoginStatus.LockedOut));
            case AdminLoginStatus.Succeeded when result.Claims is { } claims:
                var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));
                var usedSecondFactor = principal.HasClaim(
                    AdminAuthenticationDefaults.AuthenticationMethodClaimType,
                    AdminAuthenticationDefaults.MultiFactorAuthenticationMethod);
                if (this._authOptions.Value.RequireTwoFactor && !usedSecondFactor)
                {
                    // The second factor has to be set up in the admin panel first.
                    return this.StatusCode(StatusCodes.Status403Forbidden, new LoginResponse(AdminApiLoginStatus.TwoFactorSetupRequired));
                }

                var payload = new AdminTokenPayload(
                    Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!),
                    principal.FindFirstValue(AdminAuthenticationDefaults.SecurityStampClaimType)!,
                    usedSecondFactor);
                return this.Ok(new LoginResponse(
                    AdminApiLoginStatus.Succeeded,
                    this._tokenService.Issue(payload),
                    DateTime.UtcNow + AdminTokenService.TokenLifetime,
                    CreateUserInfo(principal)));
            default:
                return this.Unauthorized(new LoginResponse(AdminApiLoginStatus.Failed));
        }
    }

    /// <summary>
    /// Gets whether a login is required.
    /// </summary>
    /// <param name="userAvailability">The service which knows if any admin user exists.</param>
    /// <returns>The status.</returns>
    /// <remarks>
    /// As long as no admin user exists, the admin panel runs in its initial setup mode without a login,
    /// see <see cref="AdminAccessRequirementHandler"/>. The admin API behaves the same way.
    /// </remarks>
    [HttpGet("status")]
    [AllowAnonymous]
    public async Task<AuthStatus> GetStatusAsync([FromServices] AdminUserAvailabilityService userAvailability)
    {
        return new AuthStatus(await userAvailability.AnyUserExistsAsync(this.HttpContext.RequestAborted).ConfigureAwait(false));
    }

    /// <summary>
    /// Gets the currently authenticated user.
    /// </summary>
    /// <returns>The user.</returns>
    /// <remarks>
    /// In the initial setup mode, the request is authorized without a user; it then gets all roles,
    /// like in the admin panel.
    /// </remarks>
    [HttpGet("me")]
    public ActionResult<AdminUserInfo> GetCurrentUser()
    {
        if (this.User.Identity?.IsAuthenticated is not true)
        {
            return new AdminUserInfo(string.Empty, AdminRoles.Administrator, AdminRoles.All, false, true);
        }

        return CreateUserInfo(this.User);
    }

    private static AdminUserInfo CreateUserInfo(ClaimsPrincipal principal)
    {
        var roles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        var highestRole = AdminRoles.All.LastOrDefault(r => roles.Contains(r, StringComparer.OrdinalIgnoreCase));
        return new AdminUserInfo(
            principal.FindFirstValue(ClaimTypes.Name) ?? string.Empty,
            highestRole,
            roles,
            principal.HasClaim(
                AdminAuthenticationDefaults.AuthenticationMethodClaimType,
                AdminAuthenticationDefaults.MultiFactorAuthenticationMethod));
    }

    /// <summary>
    /// Describes whether a login is required.
    /// </summary>
    /// <param name="LoginRequired">If set to <c>false</c>, no admin user exists yet and the API is accessible without a login.</param>
    public record AuthStatus(bool LoginRequired);

    /// <summary>
    /// The login request.
    /// </summary>
    /// <param name="LoginName">The login name.</param>
    /// <param name="Password">The password.</param>
    /// <param name="Code">The code of the authenticator app, or a recovery code.</param>
    /// <param name="IsRecoveryCode">If set to <c>true</c>, the <paramref name="Code"/> is a recovery code.</param>
    public record LoginRequest(string LoginName, string Password, string? Code = null, bool IsRecoveryCode = false);

    /// <summary>
    /// The response of a login attempt.
    /// </summary>
    /// <param name="Status">The status.</param>
    /// <param name="Token">The token, if the login succeeded.</param>
    /// <param name="ExpiresAt">The expiration of the token.</param>
    /// <param name="User">The user, if the login succeeded.</param>
    public record LoginResponse(AdminApiLoginStatus Status, string? Token = null, DateTime? ExpiresAt = null, AdminUserInfo? User = null);

    /// <summary>
    /// Information about the authenticated admin user.
    /// </summary>
    /// <param name="LoginName">The login name.</param>
    /// <param name="Role">The highest role of the user.</param>
    /// <param name="Roles">All effective roles of the user.</param>
    /// <param name="UsedSecondFactor">If set to <c>true</c>, the user authenticated with a second factor.</param>
    /// <param name="IsSetupMode">If set to <c>true</c>, no admin user exists yet and the access isn't protected.</param>
    public record AdminUserInfo(string LoginName, string? Role, IReadOnlyList<string> Roles, bool UsedSecondFactor, bool IsSetupMode = false);
}
