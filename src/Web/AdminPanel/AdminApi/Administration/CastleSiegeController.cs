// <copyright file="CastleSiegeController.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AdminApi.Administration;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.CastleSiege;
using MUnique.OpenMU.Web.AdminPanel.AdminApi.Audit;
using MUnique.OpenMU.Web.AdminPanel.AdminApi.Configuration;
using MUnique.OpenMU.Web.AdminPanel.Auth;
using MUnique.OpenMU.Web.AdminPanel.Services;

/// <summary>
/// Manages the castle siege of a game server, like the castle siege page of the admin panel
/// (see <see cref="CastleSiegeManagementService"/>).
/// </summary>
[ApiController]
[Route(AdminApiDefaults.RoutePrefix + "/castle-siege")]
[Authorize(AuthenticationSchemes = AdminApiDefaults.AuthenticationScheme, Policy = AdminPolicies.Administrator)]
public class CastleSiegeController : ControllerBase
{
    private static readonly IReadOnlyDictionary<CastleSiegeAdministrationError, string> ErrorMessages = new Dictionary<CastleSiegeAdministrationError, string>
    {
        { CastleSiegeAdministrationError.InvalidState, "Estado inválido." },
        { CastleSiegeAdministrationError.NotInitialized, "El castle siege todavía no se inicializó en este servidor." },
        { CastleSiegeAdministrationError.GuildNameRequired, "Falta el nombre del guild." },
        { CastleSiegeAdministrationError.GameServerContextRequired, "El servidor de juego no está disponible." },
        { CastleSiegeAdministrationError.GuildNotFound, "No existe un guild con ese nombre." },
        { CastleSiegeAdministrationError.OwnerChangeDuringBattle, "No se puede cambiar el dueño durante la batalla." },
        { CastleSiegeAdministrationError.ResetDuringActiveSiege, "No se puede reiniciar el ciclo con el siege en curso." },
        { CastleSiegeAdministrationError.TaxOutOfRange, "Los impuestos están fuera del rango permitido." },
        { CastleSiegeAdministrationError.TaxChangeDuringBattle, "No se pueden cambiar los impuestos durante la batalla." },
        { CastleSiegeAdministrationError.TributeClearDuringBattle, "No se puede vaciar el tributo durante la batalla." },
        { CastleSiegeAdministrationError.RegistrationChangeOutsideRegistration, "Las inscripciones solo se pueden cambiar durante el período de inscripción." },
        { CastleSiegeAdministrationError.RegistrationMissing, "Ese guild no está inscripto." },
        { CastleSiegeAdministrationError.GameServerUnavailable, "El servidor de juego no está disponible." },
        { CastleSiegeAdministrationError.AllInOneDeploymentRequired, "Solo se puede administrar con el servidor todo-en-uno." },
        { CastleSiegeAdministrationError.PlugInInactive, "El plugin del castle siege no está activo." },
    };

    private readonly CastleSiegeManagementService _service;
    private readonly AdminAuditLog _auditLog;

    /// <summary>
    /// Initializes a new instance of the <see cref="CastleSiegeController"/> class.
    /// </summary>
    /// <param name="service">The castle siege management service.</param>
    /// <param name="auditLog">The audit log.</param>
    public CastleSiegeController(CastleSiegeManagementService service, AdminAuditLog auditLog)
    {
        this._service = service;
        this._auditLog = auditLog;
    }

    /// <summary>
    /// Gets the game servers whose castle siege can be managed, and the states of a siege.
    /// </summary>
    /// <returns>The servers and states.</returns>
    [HttpGet]
    public CastleSiegeOverview GetOverview()
    {
        return new CastleSiegeOverview(
            this._service.AvailableGameServers.Select(s => new GameServerItem(s.Id, s.Description)).ToList(),
            Enum.GetNames<CastleSiegeState>());
    }

    /// <summary>
    /// Gets the castle siege of a game server.
    /// </summary>
    /// <param name="serverId">The id of the game server.</param>
    /// <returns>The castle siege.</returns>
    [HttpGet("{serverId:int}")]
    public async Task<ActionResult<CastleSiegeAdministrationSnapshot>> GetSnapshotAsync(int serverId)
    {
        var result = await this._service.GetSnapshotAsync(serverId).ConfigureAwait(false);
        return result.Snapshot is { } snapshot ? snapshot : this.Conflict(Error(result.Error));
    }

    /// <summary>
    /// Forces a state of the siege, e.g. to start it now.
    /// </summary>
    /// <param name="serverId">The id of the game server.</param>
    /// <param name="request">The state.</param>
    /// <returns>The castle siege.</returns>
    [HttpPost("{serverId:int}/state")]
    public async Task<ActionResult<CastleSiegeAdministrationSnapshot>> ForceStateAsync(int serverId, [FromBody] StateRequest request)
    {
        if (!Enum.TryParse<CastleSiegeState>(request.State, out var state))
        {
            return this.BadRequest(Error(CastleSiegeAdministrationError.InvalidState));
        }

        return await this.RunAsync(serverId, "Estado forzado", state.ToString(), () => this._service.ForceStateAsync(serverId, state)).ConfigureAwait(false);
    }

    /// <summary>
    /// Sets the guild which owns the castle.
    /// </summary>
    /// <param name="serverId">The id of the game server.</param>
    /// <param name="request">The name of the guild.</param>
    /// <returns>The castle siege.</returns>
    [HttpPost("{serverId:int}/owner")]
    public Task<ActionResult<CastleSiegeAdministrationSnapshot>> SetOwnerAsync(int serverId, [FromBody] OwnerRequest request) =>
        this.RunAsync(serverId, "Dueño", request.GuildName, () => this._service.SetOwnerAsync(serverId, request.GuildName?.Trim() ?? string.Empty));

    /// <summary>
    /// Starts the cycle of the siege over.
    /// </summary>
    /// <param name="serverId">The id of the game server.</param>
    /// <returns>The castle siege.</returns>
    [HttpPost("{serverId:int}/reset")]
    public Task<ActionResult<CastleSiegeAdministrationSnapshot>> ResetCycleAsync(int serverId) =>
        this.RunAsync(serverId, "Ciclo reiniciado", null, () => this._service.ResetCycleAsync(serverId));

    /// <summary>
    /// Sets the taxes of the castle.
    /// </summary>
    /// <param name="serverId">The id of the game server.</param>
    /// <param name="request">The taxes.</param>
    /// <returns>The castle siege.</returns>
    [HttpPost("{serverId:int}/taxes")]
    public Task<ActionResult<CastleSiegeAdministrationSnapshot>> SetTaxesAsync(int serverId, [FromBody] TaxesRequest request) =>
        this.RunAsync(
            serverId,
            "Impuestos",
            $"Chaos {request.ChaosTax} · Tienda {request.StoreTax} · Caza {request.HuntTax}",
            () => this._service.SetTaxesAsync(serverId, request.ChaosTax, request.StoreTax, request.HuntTax));

    /// <summary>
    /// Empties the collected tribute.
    /// </summary>
    /// <param name="serverId">The id of the game server.</param>
    /// <returns>The castle siege.</returns>
    [HttpPost("{serverId:int}/clear-tribute")]
    public Task<ActionResult<CastleSiegeAdministrationSnapshot>> ClearTributeAsync(int serverId) =>
        this.RunAsync(serverId, "Tributo vaciado", null, () => this._service.ClearTributeAsync(serverId));

    /// <summary>
    /// Removes the registration of a guild.
    /// </summary>
    /// <param name="serverId">The id of the game server.</param>
    /// <param name="guildId">The id of the guild.</param>
    /// <returns>The castle siege.</returns>
    [HttpDelete("{serverId:int}/registrations/{guildId:guid}")]
    public Task<ActionResult<CastleSiegeAdministrationSnapshot>> RemoveRegistrationAsync(int serverId, Guid guildId) =>
        this.RunAsync(serverId, "Inscripción quitada", guildId.ToString(), () => this._service.RemoveRegistrationAsync(serverId, guildId));

    private static ConfigurationEditController.ErrorResponse Error(CastleSiegeAdministrationError error) =>
        new(ErrorMessages.GetValueOrDefault(error, error.ToString()));

    private async Task<ActionResult<CastleSiegeAdministrationSnapshot>> RunAsync(int serverId, string caption, string? value, Func<ValueTask<CastleSiegeAdministrationResult>> action)
    {
        var result = await action().ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return this.BadRequest(Error(result.Error));
        }

        await this._auditLog.AddAsync(new AuditEntry(
            DateTimeOffset.UtcNow,
            this.User.Identity?.Name,
            AuditAction.Updated,
            "CastleSiege",
            "Castle siege",
            Guid.Empty,
            $"Server {serverId}",
            [new AuditChange(caption, caption, null, value)])).ConfigureAwait(false);
        return await this.GetSnapshotAsync(serverId).ConfigureAwait(false);
    }

    /// <summary>
    /// The game servers and the states of a siege.
    /// </summary>
    /// <param name="Servers">The game servers.</param>
    /// <param name="States">The states of a siege, in their order.</param>
    public record CastleSiegeOverview(IReadOnlyList<GameServerItem> Servers, IReadOnlyList<string> States);

    /// <summary>
    /// A game server.
    /// </summary>
    /// <param name="Id">The id.</param>
    /// <param name="Description">The description.</param>
    public record GameServerItem(int Id, string Description);

    /// <summary>
    /// A state of the siege.
    /// </summary>
    /// <param name="State">The state.</param>
    public record StateRequest(string? State);

    /// <summary>
    /// The owner of the castle.
    /// </summary>
    /// <param name="GuildName">The name of the guild.</param>
    public record OwnerRequest(string? GuildName);

    /// <summary>
    /// The taxes of the castle.
    /// </summary>
    /// <param name="ChaosTax">The tax of the chaos machine.</param>
    /// <param name="StoreTax">The tax of the stores.</param>
    /// <param name="HuntTax">The tax of the hunting zone.</param>
    public record TaxesRequest(byte ChaosTax, byte StoreTax, int HuntTax);
}
