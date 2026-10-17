// <copyright file="CosmeticSetsController.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AdminApi.Administration;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MUnique.OpenMU.Web.AdminPanel.AdminApi.Audit;
using MUnique.OpenMU.Web.AdminPanel.AdminApi.Configuration;
using MUnique.OpenMU.Web.AdminPanel.Auth;
using MUnique.OpenMU.Web.AdminPanel.Services;

/// <summary>
/// The cosmetic sets of the characters: which ones they unlocked and wear, and giving or removing them.
/// </summary>
[ApiController]
[Route(AdminApiDefaults.RoutePrefix + "/characters/{characterId:guid}/cosmetic-sets")]
[Authorize(AuthenticationSchemes = AdminApiDefaults.AuthenticationScheme, Policy = AdminPolicies.Viewer)]
public class CosmeticSetsController : ControllerBase
{
    private const string AuditEntityType = "CosmeticSet";

    private const string AuditEntityCaption = "Set cosmético";

    private static readonly IReadOnlyDictionary<CosmeticSetAdministrationError, string> ErrorMessages = new Dictionary<CosmeticSetAdministrationError, string>
    {
        { CosmeticSetAdministrationError.PlugInInactive, "El plugin de los sets cosméticos no está activo." },
        { CosmeticSetAdministrationError.SetNotFound, "No existe un set cosmético con ese número." },
        { CosmeticSetAdministrationError.NotUnlocked, "El personaje no tiene ese set cosmético." },
        { CosmeticSetAdministrationError.StorageUnavailable, "No se pudo acceder a la base de datos de progresión." },
    };

    private readonly CosmeticSetManagementService _service;
    private readonly AdminAuditLog _auditLog;

    /// <summary>
    /// Initializes a new instance of the <see cref="CosmeticSetsController"/> class.
    /// </summary>
    /// <param name="service">The service of the cosmetic sets.</param>
    /// <param name="auditLog">The audit log.</param>
    public CosmeticSetsController(CosmeticSetManagementService service, AdminAuditLog auditLog)
    {
        this._service = service;
        this._auditLog = auditLog;
    }

    /// <summary>
    /// Gets the configured cosmetic sets, with the ones which the character unlocked and wears.
    /// </summary>
    /// <param name="characterId">The identifier of the character.</param>
    /// <returns>The sets.</returns>
    [HttpGet]
    public async Task<ActionResult<CharacterCosmeticSets>> GetAsync(Guid characterId)
    {
        var (sets, error) = await this._service.GetAsync(characterId).ConfigureAwait(false);
        return sets is null ? this.Conflict(Error(error)) : this.Ok(sets);
    }

    /// <summary>
    /// Unlocks a cosmetic set for the character.
    /// </summary>
    /// <param name="characterId">The identifier of the character.</param>
    /// <param name="setNumber">The number of the set.</param>
    /// <returns>The sets after the change.</returns>
    [HttpPost("{setNumber:int}")]
    [Authorize(AuthenticationSchemes = AdminApiDefaults.AuthenticationScheme, Policy = AdminPolicies.Operator)]
    public async Task<ActionResult<CharacterCosmeticSets>> GrantAsync(Guid characterId, short setNumber)
    {
        var error = await this._service.GrantAsync(characterId, setNumber).ConfigureAwait(false);
        return await this.CompleteAsync(characterId, setNumber, error, "Otorgado").ConfigureAwait(false);
    }

    /// <summary>
    /// Removes an unlocked cosmetic set from the character.
    /// </summary>
    /// <param name="characterId">The identifier of the character.</param>
    /// <param name="setNumber">The number of the set.</param>
    /// <returns>The sets after the change.</returns>
    [HttpDelete("{setNumber:int}")]
    [Authorize(AuthenticationSchemes = AdminApiDefaults.AuthenticationScheme, Policy = AdminPolicies.Operator)]
    public async Task<ActionResult<CharacterCosmeticSets>> RevokeAsync(Guid characterId, short setNumber)
    {
        var error = await this._service.RevokeAsync(characterId, setNumber).ConfigureAwait(false);
        return await this.CompleteAsync(characterId, setNumber, error, "Quitado").ConfigureAwait(false);
    }

    private static ConfigurationEditController.ErrorResponse Error(CosmeticSetAdministrationError error) =>
        new(ErrorMessages.GetValueOrDefault(error, error.ToString()));

    private async Task<ActionResult<CharacterCosmeticSets>> CompleteAsync(Guid characterId, short setNumber, CosmeticSetAdministrationError error, string caption)
    {
        if (error != CosmeticSetAdministrationError.None)
        {
            return this.BadRequest(Error(error));
        }

        await this._auditLog.AddAsync(new AuditEntry(
            DateTimeOffset.UtcNow,
            this.User.Identity?.Name,
            AuditAction.Updated,
            AuditEntityType,
            AuditEntityCaption,
            characterId,
            $"Set {setNumber}",
            [new AuditChange(caption, caption, null, setNumber.ToString(System.Globalization.CultureInfo.InvariantCulture))])).ConfigureAwait(false);
        return await this.GetAsync(characterId).ConfigureAwait(false);
    }
}
