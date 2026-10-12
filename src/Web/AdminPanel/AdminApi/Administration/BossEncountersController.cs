// <copyright file="BossEncountersController.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AdminApi.Administration;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MUnique.OpenMU.GameLogic.PlugIns.BossEncounters;
using MUnique.OpenMU.Web.AdminPanel.AdminApi.Audit;
using MUnique.OpenMU.Web.AdminPanel.AdminApi.Configuration;
using MUnique.OpenMU.Web.AdminPanel.Auth;
using MUnique.OpenMU.Web.AdminPanel.Services;

/// <summary>
/// The boss encounters of the game servers: their state, starting and ending them, and their history.
/// </summary>
[ApiController]
[Route(AdminApiDefaults.RoutePrefix + "/boss-encounters")]
[Authorize(AuthenticationSchemes = AdminApiDefaults.AuthenticationScheme, Policy = AdminPolicies.Viewer)]
public class BossEncountersController : ControllerBase
{
    private const int DefaultHistorySize = 50;

    private static readonly IReadOnlyDictionary<BossEncounterAdministrationError, string> ErrorMessages = new Dictionary<BossEncounterAdministrationError, string>
    {
        { BossEncounterAdministrationError.GameServerUnavailable, "El servidor de juego no está disponible." },
        { BossEncounterAdministrationError.AllInOneDeploymentRequired, "Solo se puede administrar con el servidor todo-en-uno." },
        { BossEncounterAdministrationError.PlugInInactive, "El plugin de los encuentros con bosses no está activo." },
        { BossEncounterAdministrationError.EncounterNotFound, "No existe un encuentro con ese nombre." },
        { BossEncounterAdministrationError.AlreadyRunning, "El encuentro ya está en curso." },
        { BossEncounterAdministrationError.NotRunning, "El encuentro no está en curso." },
    };

    private readonly BossEncounterManagementService _service;
    private readonly AdminAuditLog _auditLog;

    /// <summary>
    /// Initializes a new instance of the <see cref="BossEncountersController"/> class.
    /// </summary>
    /// <param name="service">The service of the boss encounters.</param>
    /// <param name="auditLog">The audit log.</param>
    public BossEncountersController(BossEncounterManagementService service, AdminAuditLog auditLog)
    {
        this._service = service;
        this._auditLog = auditLog;
    }

    /// <summary>
    /// Gets the game servers whose encounters can be managed, and whether the plugin is active, e.g. to show the page in the menu.
    /// </summary>
    /// <returns>The overview.</returns>
    [HttpGet]
    public Overview GetOverview()
    {
        return new Overview(
            this._service.AvailableGameServers.Select(s => new GameServerItem(s.Id, s.Description)).ToList(),
            this._service.IsPlugInActive);
    }

    /// <summary>
    /// Gets the state of the encounters of a game server.
    /// </summary>
    /// <param name="serverId">The id of the game server.</param>
    /// <returns>The states.</returns>
    [HttpGet("{serverId:int}")]
    public ActionResult<IReadOnlyList<BossEncounterSnapshot>> GetEncounters(int serverId)
    {
        var (snapshots, error) = this._service.GetSnapshots(serverId);
        return snapshots is null ? this.Conflict(Error(error)) : this.Ok(snapshots);
    }

    /// <summary>
    /// Gets the latest finished encounters of all game servers, the newest first.
    /// </summary>
    /// <param name="take">The maximum number of results.</param>
    /// <returns>The results.</returns>
    [HttpGet("history")]
    public async Task<IReadOnlyList<BossEncounterResult>> GetHistoryAsync([FromQuery] int take = DefaultHistorySize)
    {
        var limited = Math.Clamp(take, 1, BossEncounterHistory.MaximumRecentResults);
        return await this._service.GetHistoryAsync(limited).ConfigureAwait(false);
    }

    /// <summary>
    /// Starts an encounter with the next tick of its game server.
    /// </summary>
    /// <param name="serverId">The id of the game server.</param>
    /// <param name="request">The request with the name of the encounter.</param>
    /// <returns>The state of the encounters.</returns>
    [HttpPost("{serverId:int}/start")]
    [Authorize(AuthenticationSchemes = AdminApiDefaults.AuthenticationScheme, Policy = AdminPolicies.Operator)]
    public Task<ActionResult<IReadOnlyList<BossEncounterSnapshot>>> StartAsync(int serverId, [FromBody] EncounterRequest request) =>
        this.RunAsync(serverId, request, "Iniciado", this._service.Start);

    /// <summary>
    /// Ends a running encounter with the next tick of its game server; the boss retreats.
    /// </summary>
    /// <param name="serverId">The id of the game server.</param>
    /// <param name="request">The request with the name of the encounter.</param>
    /// <returns>The state of the encounters.</returns>
    [HttpPost("{serverId:int}/stop")]
    [Authorize(AuthenticationSchemes = AdminApiDefaults.AuthenticationScheme, Policy = AdminPolicies.Operator)]
    public Task<ActionResult<IReadOnlyList<BossEncounterSnapshot>>> StopAsync(int serverId, [FromBody] EncounterRequest request) =>
        this.RunAsync(serverId, request, "Terminado", this._service.Stop);

    private static ConfigurationEditController.ErrorResponse Error(BossEncounterAdministrationError error) =>
        new(ErrorMessages.GetValueOrDefault(error, error.ToString()));

    private async Task<ActionResult<IReadOnlyList<BossEncounterSnapshot>>> RunAsync(int serverId, EncounterRequest request, string caption, Func<int, string, BossEncounterAdministrationError> action)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        var error = action(serverId, name);
        if (error != BossEncounterAdministrationError.None)
        {
            return this.BadRequest(Error(error));
        }

        await this._auditLog.AddAsync(new AuditEntry(
            DateTimeOffset.UtcNow,
            this.User.Identity?.Name,
            AuditAction.Updated,
            "BossEncounter",
            "Encuentro con boss",
            Guid.Empty,
            $"{name} (servidor {serverId})",
            [new AuditChange(caption, caption, null, name)])).ConfigureAwait(false);
        return this.GetEncounters(serverId);
    }

    /// <summary>
    /// The game servers whose encounters can be managed.
    /// </summary>
    /// <param name="Servers">The game servers.</param>
    /// <param name="IsPlugInActive">A value indicating whether the plugin of the boss encounters is active.</param>
    public record Overview(IReadOnlyList<GameServerItem> Servers, bool IsPlugInActive);

    /// <summary>
    /// A game server whose encounters can be managed.
    /// </summary>
    /// <param name="Id">The id.</param>
    /// <param name="Description">The description.</param>
    public record GameServerItem(int Id, string Description);

    /// <summary>
    /// The request to start or end an encounter.
    /// </summary>
    /// <param name="Name">The name of the encounter.</param>
    public record EncounterRequest(string? Name);
}
