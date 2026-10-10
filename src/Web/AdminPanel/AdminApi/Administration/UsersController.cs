// <copyright file="UsersController.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AdminApi.Administration;

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MUnique.OpenMU.Persistence.AdminAuth;
using MUnique.OpenMU.Web.AdminPanel.AdminApi.Audit;
using MUnique.OpenMU.Web.AdminPanel.AdminApi.Configuration;
using MUnique.OpenMU.Web.AdminPanel.Auth;

/// <summary>
/// Manages the users of the admin panel, like its users page (see <c>AdminUserManagementService</c>).
/// </summary>
[ApiController]
[Route(AdminApiDefaults.RoutePrefix + "/users")]
[Authorize(AuthenticationSchemes = AdminApiDefaults.AuthenticationScheme, Policy = AdminPolicies.Administrator)]
public class UsersController : ControllerBase
{
    private readonly UserManager<AdminUser> _userManager;
    private readonly IAdminUserRepository _repository;
    private readonly AuthenticatorSetupService _authenticatorSetup;
    private readonly AdminUserAvailabilityService _userAvailability;
    private readonly AdminAuditLog _auditLog;

    /// <summary>
    /// Initializes a new instance of the <see cref="UsersController"/> class.
    /// </summary>
    /// <param name="userManager">The user manager.</param>
    /// <param name="repository">The repository of the users.</param>
    /// <param name="authenticatorSetup">The service which sets the second factor up.</param>
    /// <param name="userAvailability">The service which knows whether any user exists.</param>
    /// <param name="auditLog">The audit log.</param>
    public UsersController(
        UserManager<AdminUser> userManager,
        IAdminUserRepository repository,
        AuthenticatorSetupService authenticatorSetup,
        AdminUserAvailabilityService userAvailability,
        AdminAuditLog auditLog)
    {
        this._userManager = userManager;
        this._repository = repository;
        this._authenticatorSetup = authenticatorSetup;
        this._userAvailability = userAvailability;
        this._auditLog = auditLog;
    }

    private Guid? CurrentUserId => Guid.TryParse(this.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    /// <summary>
    /// Gets all users.
    /// </summary>
    /// <returns>The users.</returns>
    [HttpGet]
    public async Task<IEnumerable<UserInfo>> GetUsersAsync()
    {
        var users = await this._repository.GetAllAsync(this.HttpContext.RequestAborted).ConfigureAwait(false);
        return users.OrderBy(u => u.LoginName, StringComparer.OrdinalIgnoreCase).Select(this.ToInfo);
    }

    /// <summary>
    /// Creates a user. The first user ends the initial setup mode: from then on, a login is required.
    /// </summary>
    /// <param name="request">The data of the user.</param>
    /// <returns>The user.</returns>
    [HttpPost]
    public async Task<ActionResult<UserInfo>> CreateUserAsync([FromBody] CreateUserRequest request)
    {
        if (!TryParseRole(request.Role, out var role))
        {
            return this.BadRequest(new ConfigurationEditController.ErrorResponse("Rol inválido."));
        }

        var user = new AdminUser { LoginName = request.LoginName?.Trim() ?? string.Empty, Roles = role };
        var result = await this._userManager.CreateAsync(user, request.Password ?? string.Empty).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            return this.BadRequest(IdentityError(result));
        }

        this._userAvailability.Invalidate();
        await this.AuditAsync(AuditAction.Created, user, [new AuditChange("Role", "Rol", null, role)]).ConfigureAwait(false);
        return this.StatusCode(StatusCodes.Status201Created, this.ToInfo(user));
    }

    /// <summary>
    /// Assigns a role. The sessions of the user end, because they carry the old role.
    /// </summary>
    /// <param name="id">The id of the user.</param>
    /// <param name="request">The role.</param>
    /// <returns>The user.</returns>
    [HttpPost("{id:guid}/role")]
    public async Task<ActionResult<UserInfo>> SetRoleAsync(Guid id, [FromBody] RoleRequest request)
    {
        if (!TryParseRole(request.Role, out var role))
        {
            return this.BadRequest(new ConfigurationEditController.ErrorResponse("Rol inválido."));
        }

        return await this.ChangeAsync(id, async user =>
        {
            var previous = user.Roles;
            user.Roles = role;
            await this._userManager.UpdateSecurityStampAsync(user).ConfigureAwait(false);
            return new AuditChange("Role", "Rol", previous, role);
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Replaces the password of a user.
    /// </summary>
    /// <param name="id">The id of the user.</param>
    /// <param name="request">The new password.</param>
    /// <returns>The user.</returns>
    [HttpPost("{id:guid}/password")]
    public async Task<ActionResult<UserInfo>> SetPasswordAsync(Guid id, [FromBody] PasswordRequest request)
    {
        return await this.ChangeAsync(id, async user =>
        {
            var token = await this._userManager.GeneratePasswordResetTokenAsync(user).ConfigureAwait(false);
            var result = await this._userManager.ResetPasswordAsync(user, token, request.Password ?? string.Empty).ConfigureAwait(false);
            return result.Succeeded ? new AuditChange("Password", "Contraseña", null, null) : throw new IdentityException(result);
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Removes the second factor of a user, e.g. when it lost its authenticator app.
    /// </summary>
    /// <param name="id">The id of the user.</param>
    /// <returns>The user.</returns>
    [HttpPost("{id:guid}/reset-two-factor")]
    public async Task<ActionResult<UserInfo>> ResetTwoFactorAsync(Guid id)
    {
        return await this.ChangeAsync(id, async user =>
        {
            await this._authenticatorSetup.DisableAsync(user).ConfigureAwait(false);
            return new AuditChange("TwoFactor", "Segundo factor", "Activo", "Quitado");
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Disables or enables a user. A disabled user can't log in, and its sessions end.
    /// </summary>
    /// <param name="id">The id of the user.</param>
    /// <param name="request">Whether the user is disabled.</param>
    /// <returns>The user.</returns>
    [HttpPost("{id:guid}/disabled")]
    public async Task<ActionResult<UserInfo>> SetDisabledAsync(Guid id, [FromBody] DisabledRequest request)
    {
        if (request.IsDisabled && id == this.CurrentUserId)
        {
            return this.BadRequest(new ConfigurationEditController.ErrorResponse("No podés deshabilitar tu propio usuario."));
        }

        return await this.ChangeAsync(id, async user =>
        {
            user.IsDisabled = request.IsDisabled;
            await this._userManager.UpdateSecurityStampAsync(user).ConfigureAwait(false);
            return new AuditChange("IsDisabled", "Estado", null, request.IsDisabled ? "Deshabilitado" : "Habilitado");
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes a user. The last user and the own user can't be deleted.
    /// </summary>
    /// <param name="id">The id of the user.</param>
    /// <returns>The result.</returns>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteUserAsync(Guid id)
    {
        if (id == this.CurrentUserId)
        {
            return this.BadRequest(new ConfigurationEditController.ErrorResponse("No podés borrar tu propio usuario."));
        }

        if (await this.FindEditableAsync(id).ConfigureAwait(false) is not { } user)
        {
            return this.NotFound();
        }

        if (BootstrapAdminUserProvider.IsBootstrapUser(user))
        {
            return this.BadRequest(BootstrapError());
        }

        if (await this._repository.GetCountAsync(this.HttpContext.RequestAborted).ConfigureAwait(false) <= 1)
        {
            return this.BadRequest(new ConfigurationEditController.ErrorResponse("No se puede borrar el último usuario."));
        }

        var result = await this._userManager.DeleteAsync(user).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            return this.BadRequest(IdentityError(result));
        }

        this._userAvailability.Invalidate();
        await this.AuditAsync(AuditAction.Deleted, user, []).ConfigureAwait(false);
        return this.NoContent();
    }

    private static bool TryParseRole(string? value, out string role)
    {
        role = Enum.TryParse<AdminRole>(value, true, out var parsed) && Enum.IsDefined(parsed) ? parsed.ToString() : string.Empty;
        return role.Length > 0;
    }

    private static ConfigurationEditController.ErrorResponse IdentityError(IdentityResult result) =>
        new(string.Join(" ", result.Errors.Select(e => e.Description)));

    private static ConfigurationEditController.ErrorResponse BootstrapError() =>
        new("Este usuario viene de la configuración del servidor y no se puede modificar desde el panel.");

    private async Task<AdminUser?> FindEditableAsync(Guid id) =>
        await this._repository.GetByIdAsync(id, this.HttpContext.RequestAborted).ConfigureAwait(false);

    private async Task<ActionResult<UserInfo>> ChangeAsync(Guid id, Func<AdminUser, Task<AuditChange>> change)
    {
        if (await this.FindEditableAsync(id).ConfigureAwait(false) is not { } user)
        {
            return this.NotFound();
        }

        if (BootstrapAdminUserProvider.IsBootstrapUser(user))
        {
            return this.BadRequest(BootstrapError());
        }

        AuditChange auditChange;
        try
        {
            auditChange = await change(user).ConfigureAwait(false);
        }
        catch (IdentityException ex)
        {
            return this.BadRequest(IdentityError(ex.Result));
        }

        await this.AuditAsync(AuditAction.Updated, user, [auditChange]).ConfigureAwait(false);
        return this.ToInfo(user);
    }

    private UserInfo ToInfo(AdminUser user) => new(
        user.Id,
        user.LoginName,
        user.Roles,
        user.IsTwoFactorEnabled,
        user.IsDisabled,
        user.LockoutEnd is { } lockoutEnd && lockoutEnd > DateTimeOffset.UtcNow,
        user.CreatedAt,
        user.LastLoginAt,
        BootstrapAdminUserProvider.IsBootstrapUser(user),
        user.Id == this.CurrentUserId);

    private Task AuditAsync(AuditAction action, AdminUser user, IReadOnlyList<AuditChange> changes)
    {
        return this._auditLog.AddAsync(new AuditEntry(
            DateTimeOffset.UtcNow,
            this.User.Identity?.Name,
            action,
            "AdminUser",
            "Usuarios del panel",
            user.Id,
            user.LoginName,
            changes));
    }

    /// <summary>
    /// A user of the admin panel.
    /// </summary>
    /// <param name="Id">The id.</param>
    /// <param name="LoginName">The login name.</param>
    /// <param name="Role">The role.</param>
    /// <param name="IsTwoFactorEnabled">If set to <c>true</c>, the user logs in with a second factor.</param>
    /// <param name="IsDisabled">If set to <c>true</c>, the user can't log in.</param>
    /// <param name="IsLockedOut">If set to <c>true</c>, the user is locked out after failed logins.</param>
    /// <param name="CreatedAt">When the user was created.</param>
    /// <param name="LastLoginAt">When the user logged in the last time.</param>
    /// <param name="IsBootstrap">If set to <c>true</c>, the user comes from the configuration of the server and can't be changed.</param>
    /// <param name="IsCurrent">If set to <c>true</c>, it's the user of the request.</param>
    public record UserInfo(Guid Id, string LoginName, string Role, bool IsTwoFactorEnabled, bool IsDisabled, bool IsLockedOut, DateTime CreatedAt, DateTime? LastLoginAt, bool IsBootstrap, bool IsCurrent);

    /// <summary>
    /// The data of a new user.
    /// </summary>
    /// <param name="LoginName">The login name.</param>
    /// <param name="Password">The password.</param>
    /// <param name="Role">The role: <c>Viewer</c>, <c>Operator</c> or <c>Administrator</c>.</param>
    public record CreateUserRequest(string? LoginName, string? Password, string? Role);

    /// <summary>
    /// A role.
    /// </summary>
    /// <param name="Role">The role.</param>
    public record RoleRequest(string? Role);

    /// <summary>
    /// A password.
    /// </summary>
    /// <param name="Password">The password.</param>
    public record PasswordRequest(string? Password);

    /// <summary>
    /// Whether a user is disabled.
    /// </summary>
    /// <param name="IsDisabled">If set to <c>true</c>, the user is disabled.</param>
    public record DisabledRequest(bool IsDisabled);

    /// <summary>
    /// A failed identity operation, which is reported to the frontend.
    /// </summary>
    private sealed class IdentityException : Exception
    {
        public IdentityException(IdentityResult result)
            : base(string.Join(" ", result.Errors.Select(e => e.Description)))
        {
            this.Result = result;
        }

        public IdentityResult Result { get; }
    }
}
