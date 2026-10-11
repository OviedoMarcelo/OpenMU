// <copyright file="BossEncounterManagementService.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.Services;

using Microsoft.Extensions.Logging;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlugIns.BossEncounters;
using MUnique.OpenMU.Interfaces;

/// <summary>
/// Gives the admin panel access to the boss encounters of the game servers in this process: their state,
/// starting and ending them, and the history of the finished ones.
/// </summary>
/// <remarks>
/// Like the castle siege, the state of the encounters isn't transported over the public administration API,
/// so only game servers which provide their local <see cref="IGameServerContext"/> are available.
/// </remarks>
public sealed class BossEncounterManagementService
{
    private readonly IServerProvider _serverProvider;
    private readonly ILogger<BossEncounterManagementService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BossEncounterManagementService"/> class.
    /// </summary>
    /// <param name="serverProvider">The provider of manageable servers.</param>
    /// <param name="logger">The logger.</param>
    public BossEncounterManagementService(IServerProvider serverProvider, ILogger<BossEncounterManagementService> logger)
    {
        this._serverProvider = serverProvider;
        this._logger = logger;
        this.AvailableGameServers = serverProvider.Servers
            .OfType<IGameServer>()
            .Where(server => server is IGameServerContextProvider)
            .OrderBy(server => server.Id)
            .Select(server => new CastleSiegeManagementGameServer(server.Id, server.Description))
            .ToList();
    }

    /// <summary>
    /// Gets the game servers whose encounters can be reached.
    /// </summary>
    public IReadOnlyList<CastleSiegeManagementGameServer> AvailableGameServers { get; }

    /// <summary>
    /// Gets the state of the encounters of a game server.
    /// </summary>
    /// <param name="gameServerId">The id of the game server.</param>
    /// <returns>The states, or the error.</returns>
    public (IReadOnlyList<BossEncounterSnapshot>? Snapshots, BossEncounterAdministrationError Error) GetSnapshots(int gameServerId)
    {
        var (encounters, error) = this.Resolve(gameServerId);
        return encounters is null ? (null, error) : (encounters.Select(e => e.GetSnapshot()).ToList(), BossEncounterAdministrationError.None);
    }

    /// <summary>
    /// Starts an encounter with the next tick of its game server.
    /// </summary>
    /// <param name="gameServerId">The id of the game server.</param>
    /// <param name="name">The name of the encounter.</param>
    /// <returns>The error; <see cref="BossEncounterAdministrationError.None"/>, if it starts.</returns>
    public BossEncounterAdministrationError Start(int gameServerId, string name)
    {
        var (encounter, error) = this.Find(gameServerId, name);
        if (encounter is null)
        {
            return error;
        }

        if (encounter.IsRunning)
        {
            return BossEncounterAdministrationError.AlreadyRunning;
        }

        encounter.RequestStart();
        return BossEncounterAdministrationError.None;
    }

    /// <summary>
    /// Ends a running encounter with the next tick of its game server; the boss retreats.
    /// </summary>
    /// <param name="gameServerId">The id of the game server.</param>
    /// <param name="name">The name of the encounter.</param>
    /// <returns>The error; <see cref="BossEncounterAdministrationError.None"/>, if it ends.</returns>
    public BossEncounterAdministrationError Stop(int gameServerId, string name)
    {
        var (encounter, error) = this.Find(gameServerId, name);
        if (encounter is null)
        {
            return error;
        }

        if (!encounter.IsRunning)
        {
            return BossEncounterAdministrationError.NotRunning;
        }

        encounter.RequestStop();
        return BossEncounterAdministrationError.None;
    }

    /// <summary>
    /// Gets the latest finished encounters of all game servers, the newest first.
    /// </summary>
    /// <param name="take">The maximum number of results.</param>
    /// <returns>The results.</returns>
    public ValueTask<IReadOnlyList<BossEncounterResult>> GetHistoryAsync(int take)
    {
        return BossEncounterHistory.Shared.GetRecentAsync(take, this._logger);
    }

    private (BossEncounterContext? Encounter, BossEncounterAdministrationError Error) Find(int gameServerId, string name)
    {
        var (encounters, error) = this.Resolve(gameServerId);
        if (encounters is null)
        {
            return (null, error);
        }

        var encounter = encounters.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
        return encounter is null ? (null, BossEncounterAdministrationError.EncounterNotFound) : (encounter, BossEncounterAdministrationError.None);
    }

    private (IReadOnlyList<BossEncounterContext>? Encounters, BossEncounterAdministrationError Error) Resolve(int gameServerId)
    {
        var server = this._serverProvider.Servers
            .OfType<IGameServer>()
            .FirstOrDefault(candidate => candidate.Id == gameServerId);
        if (server is null)
        {
            return (null, BossEncounterAdministrationError.GameServerUnavailable);
        }

        if (server is not IGameServerContextProvider contextProvider)
        {
            return (null, BossEncounterAdministrationError.AllInOneDeploymentRequired);
        }

        var context = contextProvider.Context;
        return context.FeaturePlugIns.GetPlugIn<BossEncounterPlugIn>() is null
            ? (null, BossEncounterAdministrationError.PlugInInactive)
            : (BossEncounterPlugIn.GetEncounters(context), BossEncounterAdministrationError.None);
    }
}
