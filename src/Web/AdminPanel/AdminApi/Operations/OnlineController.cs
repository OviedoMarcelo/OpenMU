// <copyright file="OnlineController.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AdminApi.Operations;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Web.AdminPanel.AdminApi.Audit;
using MUnique.OpenMU.Web.AdminPanel.AdminApi.Configuration;
using MUnique.OpenMU.Web.AdminPanel.Auth;
using MUnique.OpenMU.Web.Shared.Services;

/// <summary>
/// Shows who is in the game - logged in players, offline leveling sessions and bots - and lets
/// operators disconnect them or send a message to all players.
/// </summary>
[ApiController]
[Route(AdminApiDefaults.RoutePrefix + "/online")]
[Authorize(AuthenticationSchemes = AdminApiDefaults.AuthenticationScheme, Policy = AdminPolicies.Viewer)]
public class OnlineController : ControllerBase
{
    private const int MaximumMessageLength = 200;

    private readonly LoggedInAccountService _loggedIn;
    private readonly OfflineAccountService _offline;
    private readonly BotAccountService _bots;
    private readonly IServerProvider _serverProvider;
    private readonly AdminAuditLog _auditLog;
    private readonly ILogger<OnlineController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OnlineController"/> class.
    /// </summary>
    /// <param name="loggedIn">The service of the logged in accounts.</param>
    /// <param name="offline">The service of the offline leveling sessions.</param>
    /// <param name="bots">The service of the bots.</param>
    /// <param name="serverProvider">The server provider.</param>
    /// <param name="auditLog">The audit log.</param>
    /// <param name="logger">The logger.</param>
    public OnlineController(LoggedInAccountService loggedIn, OfflineAccountService offline, BotAccountService bots, IServerProvider serverProvider, AdminAuditLog auditLog, ILogger<OnlineController> logger)
    {
        this._loggedIn = loggedIn;
        this._offline = offline;
        this._bots = bots;
        this._serverProvider = serverProvider;
        this._auditLog = auditLog;
        this._logger = logger;
    }

    /// <summary>
    /// Gets everyone who is in the game.
    /// </summary>
    /// <returns>The players, offline sessions and bots.</returns>
    [HttpGet]
    public async Task<OnlineOverview> GetAsync()
    {
        var players = await this._loggedIn.GetAsync(0, int.MaxValue).ConfigureAwait(false);
        var offlineAvailable = this._offline.IsOfflevelFeatureAvailable();
        var offline = offlineAvailable ? await this._offline.GetAsync(0, int.MaxValue).ConfigureAwait(false) : [];
        var botsAvailable = this._bots.IsBotFeatureAvailable();
        var bots = botsAvailable ? await this._bots.GetAsync(0, int.MaxValue).ConfigureAwait(false) : [];
        return new OnlineOverview(
            players.Select(p => new OnlinePlayer(p.LoginName, p.Server, p.CharacterName, p.GuildName, p.GuildId, p.PartyMaster, p.PartySize, null)).ToList(),
            offlineAvailable,
            offline.Select(p => new OnlinePlayer(p.LoginName, p.ServerId, p.CharacterName, p.GuildName, null, p.PartyMaster, p.PartySize, p.StartedAt)).ToList(),
            botsAvailable,
            bots.Select(p => new OnlinePlayer(p.LoginName, p.ServerId, p.CharacterName, p.GuildName, null, p.PartyMaster, p.PartySize, p.StartedAt)).ToList());
    }

    /// <summary>
    /// Disconnects a logged in account.
    /// </summary>
    /// <param name="request">The account.</param>
    /// <returns>The result.</returns>
    [HttpPost("disconnect")]
    [Authorize(AuthenticationSchemes = AdminApiDefaults.AuthenticationScheme, Policy = AdminPolicies.Operator)]
    public async Task<IActionResult> DisconnectAsync([FromBody] SessionRequest request)
    {
        await this._loggedIn.SetAccountOfflineAsync(new LoggedInAccount(request.LoginName, request.Server)).ConfigureAwait(false);
        await this.AuditAsync("Desconectó la cuenta", request).ConfigureAwait(false);
        return this.NoContent();
    }

    /// <summary>
    /// Stops an offline leveling session.
    /// </summary>
    /// <param name="request">The account of the session.</param>
    /// <returns>The result.</returns>
    [HttpPost("offline/stop")]
    [Authorize(AuthenticationSchemes = AdminApiDefaults.AuthenticationScheme, Policy = AdminPolicies.Operator)]
    public async Task<IActionResult> StopOfflineAsync([FromBody] SessionRequest request)
    {
        await this._offline.StopOfflinePlayerAsync(new OfflineAccount(request.LoginName, request.Server, DateTime.UtcNow)).ConfigureAwait(false);
        await this.AuditAsync("Frenó el offlevel", request).ConfigureAwait(false);
        return this.NoContent();
    }

    /// <summary>
    /// Sends a message to all players of all game servers.
    /// </summary>
    /// <param name="request">The message.</param>
    /// <returns>The result.</returns>
    [HttpPost("message")]
    [Authorize(AuthenticationSchemes = AdminApiDefaults.AuthenticationScheme, Policy = AdminPolicies.Operator)]
    public async Task<IActionResult> SendMessageAsync([FromBody] MessageRequest request)
    {
        var text = request.Text?.Trim() ?? string.Empty;
        if (text.Length == 0 || text.Length > MaximumMessageLength)
        {
            return this.BadRequest(new ConfigurationEditController.ErrorResponse(
                $"El mensaje tiene que tener entre 1 y {MaximumMessageLength} caracteres.",
                [new FieldError("Text", $"Entre 1 y {MaximumMessageLength} caracteres.")]));
        }

        if (!Enum.TryParse<MessageType>(request.Type ?? nameof(MessageType.GoldenCenter), out var type))
        {
            return this.BadRequest(new ConfigurationEditController.ErrorResponse("Tipo de mensaje inválido."));
        }

        // Like the servers page of the admin panel: only to started servers, and one which fails
        // doesn't stop the others.
        var targets = this._serverProvider.Servers
            .Where(s => s.Type == ServerType.GameServer && s.ServerState == ServerState.Started && (request.Server is null || s.Id == request.Server))
            .OfType<IGameServer>()
            .ToList();
        if (targets.Count == 0)
        {
            return this.Conflict(new ConfigurationEditController.ErrorResponse("No hay servidores de juego iniciados para enviar el mensaje."));
        }

        var failed = new List<string>();
        foreach (var gameServer in targets)
        {
            try
            {
                await gameServer.SendGlobalMessageAsync(text, type).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "The global message couldn't be sent to {Server}.", ((IManageableServer)gameServer).Description);
                failed.Add(((IManageableServer)gameServer).Description);
            }
        }

        await this._auditLog.AddAsync(new AuditEntry(
            DateTimeOffset.UtcNow,
            this.User.Identity?.Name,
            AuditAction.Created,
            "GlobalMessage",
            "Mensaje global",
            Guid.Empty,
            text,
            [])).ConfigureAwait(false);
        return failed.Count == 0
            ? this.NoContent()
            : this.Conflict(new ConfigurationEditController.ErrorResponse($"No se pudo enviar a: {string.Join(", ", failed)}."));
    }

    private Task AuditAsync(string caption, SessionRequest request)
    {
        return this._auditLog.AddAsync(new AuditEntry(
            DateTimeOffset.UtcNow,
            this.User.Identity?.Name,
            AuditAction.Updated,
            "Session",
            caption,
            Guid.Empty,
            request.LoginName,
            [new AuditChange("Server", "Servidor", request.Server.ToString(System.Globalization.CultureInfo.InvariantCulture), null)]));
    }

    /// <summary>
    /// Everyone who is in the game.
    /// </summary>
    /// <param name="Players">The logged in players.</param>
    /// <param name="OfflineAvailable">If set to <c>true</c>, the offline leveling is active.</param>
    /// <param name="Offline">The offline leveling sessions.</param>
    /// <param name="BotsAvailable">If set to <c>true</c>, the bots are active.</param>
    /// <param name="Bots">The bots.</param>
    public record OnlineOverview(IReadOnlyList<OnlinePlayer> Players, bool OfflineAvailable, IReadOnlyList<OnlinePlayer> Offline, bool BotsAvailable, IReadOnlyList<OnlinePlayer> Bots);

    /// <summary>
    /// Someone who is in the game.
    /// </summary>
    /// <param name="LoginName">The login name of the account.</param>
    /// <param name="Server">The id of the game server.</param>
    /// <param name="CharacterName">The name of the character, if one is selected.</param>
    /// <param name="GuildName">The name of the guild.</param>
    /// <param name="GuildId">The id of the guild.</param>
    /// <param name="PartyMaster">The name of the party master, if the character is in a party.</param>
    /// <param name="PartySize">The size of the party.</param>
    /// <param name="StartedAt">When an offline session or bot was started.</param>
    public record OnlinePlayer(string LoginName, byte Server, string? CharacterName, string? GuildName, Guid? GuildId, string? PartyMaster, int PartySize, DateTime? StartedAt);

    /// <summary>
    /// A session of an account on a game server.
    /// </summary>
    /// <param name="LoginName">The login name.</param>
    /// <param name="Server">The id of the game server.</param>
    public record SessionRequest(string LoginName, byte Server);

    /// <summary>
    /// A message to all players.
    /// </summary>
    /// <param name="Text">The text.</param>
    /// <param name="Type">How it's shown, see <see cref="MessageType"/>; <c>GoldenCenter</c> if not set.</param>
    /// <param name="Server">The id of a game server, or <c>null</c> for all.</param>
    public record MessageRequest(string? Text, string? Type, byte? Server);
}
