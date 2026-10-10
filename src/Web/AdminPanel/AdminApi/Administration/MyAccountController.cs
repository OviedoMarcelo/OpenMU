// <copyright file="MyAccountController.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AdminApi.Administration;

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MUnique.OpenMU.Persistence.AdminAuth;
using MUnique.OpenMU.Web.AdminPanel.AdminApi.Configuration;
using MUnique.OpenMU.Web.AdminPanel.Auth;

/// <summary>
/// The own account of the logged in admin: its password and its second factor, like the account
/// security page of the admin panel.
/// </summary>
/// <remarks>
/// Changing the password or the second factor rotates the security stamp, which invalidates the
/// token of the session. These requests therefore return a new token, which replaces the old one.
/// </remarks>
[ApiController]
[Route(AdminApiDefaults.RoutePrefix + "/my-account")]
[Authorize(AuthenticationSchemes = AdminApiDefaults.AuthenticationScheme, Policy = AdminPolicies.Viewer)]
public class MyAccountController : ControllerBase
{
    private readonly UserManager<AdminUser> _userManager;
    private readonly AuthenticatorSetupService _authenticatorSetup;
    private readonly AdminTokenService _tokenService;

    /// <summary>
    /// Initializes a new instance of the <see cref="MyAccountController"/> class.
    /// </summary>
    /// <param name="userManager">The user manager.</param>
    /// <param name="authenticatorSetup">The service which sets the second factor up.</param>
    /// <param name="tokenService">The token service.</param>
    public MyAccountController(UserManager<AdminUser> userManager, AuthenticatorSetupService authenticatorSetup, AdminTokenService tokenService)
    {
        this._userManager = userManager;
        this._authenticatorSetup = authenticatorSetup;
        this._tokenService = tokenService;
    }

    private bool UsedSecondFactor => this.User.HasClaim(
        AdminAuthenticationDefaults.AuthenticationMethodClaimType,
        AdminAuthenticationDefaults.MultiFactorAuthenticationMethod);

    /// <summary>
    /// Gets the security state of the own account.
    /// </summary>
    /// <returns>The state.</returns>
    [HttpGet]
    public async Task<ActionResult<SecurityInfo>> GetAsync()
    {
        if (await this.GetUserAsync().ConfigureAwait(false) is not { } user)
        {
            return NoUser();
        }

        return new SecurityInfo(
            user.LoginName,
            user.IsTwoFactorEnabled,
            user.IsTwoFactorEnabled ? await this._authenticatorSetup.GetRemainingRecoveryCodeCountAsync(user).ConfigureAwait(false) : 0,
            BootstrapAdminUserProvider.IsBootstrapUser(user));
    }

    /// <summary>
    /// Changes the own password.
    /// </summary>
    /// <param name="request">The current and the new password.</param>
    /// <returns>A new token for the session.</returns>
    [HttpPost("password")]
    public async Task<ActionResult<TokenResponse>> ChangePasswordAsync([FromBody] ChangePasswordRequest request)
    {
        if (await this.GetEditableUserAsync().ConfigureAwait(false) is not { } user)
        {
            return NoUser();
        }

        var result = await this._userManager.ChangePasswordAsync(user, request.CurrentPassword ?? string.Empty, request.NewPassword ?? string.Empty).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            var wrongPassword = result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.PasswordMismatch));
            return this.BadRequest(new ConfigurationEditController.ErrorResponse(
                wrongPassword ? "La contraseña actual no es correcta." : string.Join(" ", result.Errors.Select(e => e.Description)),
                [new FieldError(wrongPassword ? "CurrentPassword" : "NewPassword", wrongPassword ? "No es correcta." : "No cumple los requisitos.")]));
        }

        return this.IssueToken(user, this.UsedSecondFactor);
    }

    /// <summary>
    /// Starts to set the second factor up: creates a new authenticator key. It's only enabled after
    /// the code of the authenticator app is confirmed.
    /// </summary>
    /// <returns>The data to set the authenticator app up.</returns>
    [HttpPost("two-factor/begin")]
    public async Task<ActionResult<AuthenticatorSetup>> BeginTwoFactorAsync()
    {
        if (await this.GetEditableUserAsync().ConfigureAwait(false) is not { } user)
        {
            return NoUser();
        }

        return await this._authenticatorSetup.BeginSetupAsync(user).ConfigureAwait(false);
    }

    /// <summary>
    /// Confirms the code of the authenticator app and enables the second factor.
    /// </summary>
    /// <param name="request">The code.</param>
    /// <returns>The recovery codes and a new token for the session.</returns>
    [HttpPost("two-factor/confirm")]
    public async Task<ActionResult<TwoFactorEnabledResponse>> ConfirmTwoFactorAsync([FromBody] CodeRequest request)
    {
        if (await this.GetEditableUserAsync().ConfigureAwait(false) is not { } user)
        {
            return NoUser();
        }

        if (await this._authenticatorSetup.ConfirmSetupAsync(user, request.Code ?? string.Empty).ConfigureAwait(false) is not { } recoveryCodes)
        {
            return this.BadRequest(new ConfigurationEditController.ErrorResponse("El código no es correcto.", [new FieldError("Code", "No es correcto.")]));
        }

        return new TwoFactorEnabledResponse(recoveryCodes, this.IssueToken(user, true));
    }

    /// <summary>
    /// Disables the own second factor.
    /// </summary>
    /// <returns>A new token for the session.</returns>
    [HttpPost("two-factor/disable")]
    public async Task<ActionResult<TokenResponse>> DisableTwoFactorAsync()
    {
        if (await this.GetEditableUserAsync().ConfigureAwait(false) is not { } user)
        {
            return NoUser();
        }

        await this._authenticatorSetup.DisableAsync(user).ConfigureAwait(false);
        return this.IssueToken(user, false);
    }

    /// <summary>
    /// Replaces the recovery codes.
    /// </summary>
    /// <returns>The new recovery codes.</returns>
    [HttpPost("two-factor/recovery-codes")]
    public async Task<ActionResult<IReadOnlyList<string>>> GenerateRecoveryCodesAsync()
    {
        if (await this.GetEditableUserAsync().ConfigureAwait(false) is not { IsTwoFactorEnabled: true } user)
        {
            return this.BadRequest(new ConfigurationEditController.ErrorResponse("El segundo factor no está activo."));
        }

        return this.Ok(await this._authenticatorSetup.GenerateRecoveryCodesAsync(user).ConfigureAwait(false));
    }

    private static ConflictObjectResult NoUser() =>
        new(new ConfigurationEditController.ErrorResponse("No hay un usuario logueado: el panel está en el modo de configuración inicial."));

    private async Task<AdminUser?> GetUserAsync()
    {
        var id = this.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return id is null ? null : await this._userManager.FindByIdAsync(id).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the own user, if it can be changed - the user from the configuration of the server can't.
    /// </summary>
    private async Task<AdminUser?> GetEditableUserAsync()
    {
        var user = await this.GetUserAsync().ConfigureAwait(false);
        return user is not null && !BootstrapAdminUserProvider.IsBootstrapUser(user) ? user : null;
    }

    private TokenResponse IssueToken(AdminUser user, bool usedSecondFactor) => new(
        this._tokenService.Issue(new AdminTokenPayload(user.Id, user.SecurityStamp, usedSecondFactor)),
        DateTime.UtcNow + AdminTokenService.TokenLifetime);

    /// <summary>
    /// The security state of the own account.
    /// </summary>
    /// <param name="LoginName">The login name.</param>
    /// <param name="IsTwoFactorEnabled">If set to <c>true</c>, the second factor is active.</param>
    /// <param name="RecoveryCodesLeft">The number of recovery codes which are still unused.</param>
    /// <param name="IsBootstrap">If set to <c>true</c>, the user comes from the configuration of the server and can't be changed.</param>
    public record SecurityInfo(string LoginName, bool IsTwoFactorEnabled, int RecoveryCodesLeft, bool IsBootstrap);

    /// <summary>
    /// A new token for the session.
    /// </summary>
    /// <param name="Token">The token.</param>
    /// <param name="ExpiresAt">The expiration of the token.</param>
    public record TokenResponse(string Token, DateTime ExpiresAt);

    /// <summary>
    /// The result of enabling the second factor.
    /// </summary>
    /// <param name="RecoveryCodes">The recovery codes, which are only shown now.</param>
    /// <param name="Session">A new token for the session.</param>
    public record TwoFactorEnabledResponse(IReadOnlyList<string> RecoveryCodes, TokenResponse Session);

    /// <summary>
    /// A change of the password.
    /// </summary>
    /// <param name="CurrentPassword">The current password.</param>
    /// <param name="NewPassword">The new password.</param>
    public record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);

    /// <summary>
    /// A code of the authenticator app.
    /// </summary>
    /// <param name="Code">The code.</param>
    public record CodeRequest(string? Code);
}
