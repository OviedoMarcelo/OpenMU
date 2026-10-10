// <copyright file="ApiKeysController.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AdminApi.Administration;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MUnique.OpenMU.Persistence.AdminAuth;
using MUnique.OpenMU.Web.AdminPanel.AdminApi.Audit;
using MUnique.OpenMU.Web.AdminPanel.AdminApi.Configuration;
using MUnique.OpenMU.Web.AdminPanel.Auth;

/// <summary>
/// Manages the API keys, with which other programs (e.g. the website) use the API of the admin
/// panel, like its API keys page (see <c>ApiKeyManagementService</c>).
/// </summary>
[ApiController]
[Route(AdminApiDefaults.RoutePrefix + "/api-keys")]
[Authorize(AuthenticationSchemes = AdminApiDefaults.AuthenticationScheme, Policy = AdminPolicies.Administrator)]
public class ApiKeysController : ControllerBase
{
    private readonly IApiKeyRepository _repository;
    private readonly AdminAuditLog _auditLog;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiKeysController"/> class.
    /// </summary>
    /// <param name="repository">The repository of the keys.</param>
    /// <param name="auditLog">The audit log.</param>
    public ApiKeysController(IApiKeyRepository repository, AdminAuditLog auditLog)
    {
        this._repository = repository;
        this._auditLog = auditLog;
    }

    /// <summary>
    /// Gets all keys. The keys themselves are only stored as hashes; only their prefix is known.
    /// </summary>
    /// <returns>The keys.</returns>
    [HttpGet]
    public async Task<IEnumerable<ApiKeyInfo>> GetKeysAsync()
    {
        var keys = await this._repository.GetAllAsync(this.HttpContext.RequestAborted).ConfigureAwait(false);
        return keys.OrderBy(k => k.Name, StringComparer.OrdinalIgnoreCase).Select(ToInfo);
    }

    /// <summary>
    /// Creates a key. The key is only returned now; afterwards, only its prefix is known.
    /// </summary>
    /// <param name="request">The name and the role of the key.</param>
    /// <returns>The key.</returns>
    [HttpPost]
    public async Task<ActionResult<CreatedApiKey>> CreateKeyAsync([FromBody] CreateKeyRequest request)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            return this.BadRequest(new ConfigurationEditController.ErrorResponse("Falta el nombre.", [new FieldError("Name", "Es obligatorio.")]));
        }

        if (!Enum.TryParse<AdminRole>(request.Role, true, out var role) || !Enum.IsDefined(role))
        {
            return this.BadRequest(new ConfigurationEditController.ErrorResponse("Rol inválido."));
        }

        var generatedKey = ApiKeyGenerator.GenerateKey();
        var apiKey = new ApiKey
        {
            Id = Guid.NewGuid(),
            Name = name,
            KeyHash = ApiKeyGenerator.Hash(generatedKey),
            KeyPrefix = ApiKeyGenerator.GetVisiblePrefix(generatedKey),
            Roles = role.ToString(),
        };
        await this._repository.AddAsync(apiKey, this.HttpContext.RequestAborted).ConfigureAwait(false);
        await this.AuditAsync(AuditAction.Created, apiKey, [new AuditChange("Role", "Rol", null, apiKey.Roles)]).ConfigureAwait(false);
        return this.StatusCode(StatusCodes.Status201Created, new CreatedApiKey(ToInfo(apiKey), generatedKey));
    }

    /// <summary>
    /// Disables or enables a key. A disabled key is rejected.
    /// </summary>
    /// <param name="id">The id of the key.</param>
    /// <param name="request">Whether the key is disabled.</param>
    /// <returns>The key.</returns>
    [HttpPost("{id:guid}/disabled")]
    public async Task<ActionResult<ApiKeyInfo>> SetDisabledAsync(Guid id, [FromBody] UsersController.DisabledRequest request)
    {
        if (await this.FindAsync(id).ConfigureAwait(false) is not { } apiKey)
        {
            return this.NotFound();
        }

        apiKey.IsDisabled = request.IsDisabled;
        await this._repository.UpdateAsync(apiKey, this.HttpContext.RequestAborted).ConfigureAwait(false);
        await this.AuditAsync(AuditAction.Updated, apiKey, [new AuditChange("IsDisabled", "Estado", null, request.IsDisabled ? "Deshabilitada" : "Habilitada")]).ConfigureAwait(false);
        return ToInfo(apiKey);
    }

    /// <summary>
    /// Deletes a key.
    /// </summary>
    /// <param name="id">The id of the key.</param>
    /// <returns>The result.</returns>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteKeyAsync(Guid id)
    {
        if (await this.FindAsync(id).ConfigureAwait(false) is not { } apiKey)
        {
            return this.NotFound();
        }

        await this._repository.DeleteAsync(apiKey, this.HttpContext.RequestAborted).ConfigureAwait(false);
        await this.AuditAsync(AuditAction.Deleted, apiKey, []).ConfigureAwait(false);
        return this.NoContent();
    }

    private static ApiKeyInfo ToInfo(ApiKey key) => new(key.Id, key.Name, key.KeyPrefix, key.Roles, key.IsDisabled, key.CreatedAt);

    private async Task<ApiKey?> FindAsync(Guid id) =>
        (await this._repository.GetAllAsync(this.HttpContext.RequestAborted).ConfigureAwait(false)).FirstOrDefault(k => k.Id == id);

    private Task AuditAsync(AuditAction action, ApiKey key, IReadOnlyList<AuditChange> changes)
    {
        return this._auditLog.AddAsync(new AuditEntry(
            DateTimeOffset.UtcNow,
            this.User.Identity?.Name,
            action,
            "ApiKey",
            "API keys",
            key.Id,
            key.Name,
            changes));
    }

    /// <summary>
    /// An API key, without the key itself.
    /// </summary>
    /// <param name="Id">The id.</param>
    /// <param name="Name">The name.</param>
    /// <param name="KeyPrefix">The beginning of the key, to recognize it.</param>
    /// <param name="Role">The role.</param>
    /// <param name="IsDisabled">If set to <c>true</c>, the key is rejected.</param>
    /// <param name="CreatedAt">When the key was created.</param>
    public record ApiKeyInfo(Guid Id, string Name, string KeyPrefix, string Role, bool IsDisabled, DateTime CreatedAt);

    /// <summary>
    /// A new API key, with the key itself.
    /// </summary>
    /// <param name="Info">The key.</param>
    /// <param name="Key">The key itself, which is only returned once.</param>
    public record CreatedApiKey(ApiKeyInfo Info, string Key);

    /// <summary>
    /// The data of a new key.
    /// </summary>
    /// <param name="Name">The name, e.g. what uses the key.</param>
    /// <param name="Role">The role.</param>
    public record CreateKeyRequest(string? Name, string? Role);
}
