// <copyright file="CosmeticsController.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AdminApi.Administration;

using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MUnique.OpenMU.Web.AdminPanel.AdminApi.Audit;
using MUnique.OpenMU.Web.AdminPanel.AdminApi.Configuration;
using MUnique.OpenMU.Web.AdminPanel.Auth;
using MUnique.OpenMU.Web.AdminPanel.Services;

/// <summary>
/// The cosmetic pieces of the characters: which ones they unlocked and wear, and giving or removing them.
/// </summary>
[ApiController]
[Route(AdminApiDefaults.RoutePrefix + "/characters/{characterId:guid}/cosmetics")]
[Authorize(AuthenticationSchemes = AdminApiDefaults.AuthenticationScheme, Policy = AdminPolicies.Viewer)]
public class CosmeticsController : ControllerBase
{
    private const string AuditEntityType = "CosmeticPiece";

    private const string AuditEntityCaption = "Pieza cosmética";

    private static readonly IReadOnlyDictionary<CosmeticsAdministrationError, string> ErrorMessages = new Dictionary<CosmeticsAdministrationError, string>
    {
        { CosmeticsAdministrationError.PlugInInactive, "El plugin de las piezas cosméticas no está activo." },
        { CosmeticsAdministrationError.PieceNotFound, "No existe una pieza cosmética con ese número." },
        { CosmeticsAdministrationError.NotUnlocked, "El personaje no tiene esa pieza cosmética." },
        { CosmeticsAdministrationError.StorageUnavailable, "No se pudo acceder a la base de datos de progresión." },
    };

    private readonly CosmeticsManagementService _service;
    private readonly AdminAuditLog _auditLog;

    /// <summary>
    /// Initializes a new instance of the <see cref="CosmeticsController"/> class.
    /// </summary>
    /// <param name="service">The service of the cosmetic pieces.</param>
    /// <param name="auditLog">The audit log.</param>
    public CosmeticsController(CosmeticsManagementService service, AdminAuditLog auditLog)
    {
        this._service = service;
        this._auditLog = auditLog;
    }

    /// <summary>
    /// Gets the configured cosmetic pieces, with the ones which the character unlocked and wears.
    /// </summary>
    /// <param name="characterId">The identifier of the character.</param>
    /// <returns>The pieces.</returns>
    [HttpGet]
    public async Task<ActionResult<CharacterCosmetics>> GetAsync(Guid characterId)
    {
        var (cosmetics, error) = await this._service.GetAsync(characterId).ConfigureAwait(false);
        return cosmetics is null ? this.Conflict(Error(error)) : this.Ok(cosmetics);
    }

    /// <summary>
    /// Unlocks a cosmetic piece for the character.
    /// </summary>
    /// <param name="characterId">The identifier of the character.</param>
    /// <param name="pieceNumber">The number of the piece.</param>
    /// <returns>The pieces after the change.</returns>
    [HttpPost("{pieceNumber:int}")]
    [Authorize(AuthenticationSchemes = AdminApiDefaults.AuthenticationScheme, Policy = AdminPolicies.Operator)]
    public async Task<ActionResult<CharacterCosmetics>> GrantAsync(Guid characterId, short pieceNumber)
    {
        var error = await this._service.GrantAsync(characterId, pieceNumber).ConfigureAwait(false);
        return await this.CompleteAsync(characterId, pieceNumber, error, "Otorgada").ConfigureAwait(false);
    }

    /// <summary>
    /// Removes an unlocked cosmetic piece from the character.
    /// </summary>
    /// <param name="characterId">The identifier of the character.</param>
    /// <param name="pieceNumber">The number of the piece.</param>
    /// <returns>The pieces after the change.</returns>
    [HttpDelete("{pieceNumber:int}")]
    [Authorize(AuthenticationSchemes = AdminApiDefaults.AuthenticationScheme, Policy = AdminPolicies.Operator)]
    public async Task<ActionResult<CharacterCosmetics>> RevokeAsync(Guid characterId, short pieceNumber)
    {
        var error = await this._service.RevokeAsync(characterId, pieceNumber).ConfigureAwait(false);
        return await this.CompleteAsync(characterId, pieceNumber, error, "Quitada").ConfigureAwait(false);
    }

    private static ConfigurationEditController.ErrorResponse Error(CosmeticsAdministrationError error) =>
        new(ErrorMessages.GetValueOrDefault(error, error.ToString()));

    private async Task<ActionResult<CharacterCosmetics>> CompleteAsync(Guid characterId, short pieceNumber, CosmeticsAdministrationError error, string caption)
    {
        if (error != CosmeticsAdministrationError.None)
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
            $"Pieza {pieceNumber}",
            [new AuditChange(caption, caption, null, pieceNumber.ToString(CultureInfo.InvariantCulture))])).ConfigureAwait(false);
        return await this.GetAsync(characterId).ConfigureAwait(false);
    }
}
