// <copyright file="ServersController.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AdminApi;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Web.AdminPanel.Auth;

/// <summary>
/// Lists, starts and stops the servers which run in this process.
/// </summary>
[ApiController]
[Route(AdminApiDefaults.RoutePrefix + "/servers")]
[Authorize(AuthenticationSchemes = AdminApiDefaults.AuthenticationScheme, Policy = AdminPolicies.Viewer)]
public class ServersController : ControllerBase
{
    private readonly IServerProvider _serverProvider;
    private readonly ILogger<ServersController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServersController"/> class.
    /// </summary>
    /// <param name="serverProvider">The server provider.</param>
    /// <param name="logger">The logger.</param>
    public ServersController(IServerProvider serverProvider, ILogger<ServersController> logger)
    {
        this._serverProvider = serverProvider;
        this._logger = logger;
    }

    /// <summary>
    /// Gets all servers.
    /// </summary>
    /// <returns>The servers.</returns>
    [HttpGet]
    public IEnumerable<ServerInfoDto> GetServers()
    {
        return this._serverProvider.Servers.ToList().Select(ServerInfoDto.From);
    }

    /// <summary>
    /// Starts the server with the specified configuration id.
    /// </summary>
    /// <param name="configurationId">The configuration id of the server.</param>
    /// <returns>The server after the start.</returns>
    [HttpPost("{configurationId:guid}/start")]
    [Authorize(AuthenticationSchemes = AdminApiDefaults.AuthenticationScheme, Policy = AdminPolicies.Operator)]
    public async Task<ActionResult<ServerInfoDto>> StartAsync(Guid configurationId)
    {
        if (this.FindServer(configurationId) is not { } server)
        {
            return this.NotFound();
        }

        this._logger.LogInformation("Admin '{Admin}' starts the server '{Server}'.", this.User.Identity?.Name, server.Description);
        await server.StartAsync().ConfigureAwait(false);
        return ServerInfoDto.From(server);
    }

    /// <summary>
    /// Stops the server with the specified configuration id.
    /// </summary>
    /// <param name="configurationId">The configuration id of the server.</param>
    /// <returns>The server after the shutdown.</returns>
    [HttpPost("{configurationId:guid}/stop")]
    [Authorize(AuthenticationSchemes = AdminApiDefaults.AuthenticationScheme, Policy = AdminPolicies.Operator)]
    public async Task<ActionResult<ServerInfoDto>> StopAsync(Guid configurationId)
    {
        if (this.FindServer(configurationId) is not { } server)
        {
            return this.NotFound();
        }

        this._logger.LogInformation("Admin '{Admin}' stops the server '{Server}'.", this.User.Identity?.Name, server.Description);
        await server.ShutdownAsync().ConfigureAwait(false);
        return ServerInfoDto.From(server);
    }

    private IManageableServer? FindServer(Guid configurationId)
    {
        return this._serverProvider.Servers.ToList().FirstOrDefault(s => s.ConfigurationId == configurationId);
    }

    /// <summary>
    /// Describes a server.
    /// </summary>
    /// <param name="Id">The id.</param>
    /// <param name="ConfigurationId">The id of the configuration of the server.</param>
    /// <param name="Description">The description.</param>
    /// <param name="Type">The type, e.g. <c>GameServer</c>.</param>
    /// <param name="State">The state, e.g. <c>Started</c>.</param>
    /// <param name="CurrentConnections">The number of current connections.</param>
    /// <param name="MaximumConnections">The maximum number of connections.</param>
    public record ServerInfoDto(
        int Id,
        Guid ConfigurationId,
        string Description,
        string Type,
        string State,
        int CurrentConnections,
        int MaximumConnections)
    {
        /// <summary>
        /// Creates the description of the specified server.
        /// </summary>
        /// <param name="server">The server.</param>
        /// <returns>The description.</returns>
        public static ServerInfoDto From(IManageableServer server) => new(
            server.Id,
            server.ConfigurationId,
            server.Description,
            server.Type.ToString(),
            server.ServerState.ToString(),
            server.CurrentConnections,
            server.MaximumConnections);
    }
}
