// <copyright file="DashboardController.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AdminApi;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Web.AdminPanel.Auth;

/// <summary>
/// Provides the numbers of the dashboard.
/// </summary>
[ApiController]
[Route(AdminApiDefaults.RoutePrefix + "/dashboard")]
[Authorize(AuthenticationSchemes = AdminApiDefaults.AuthenticationScheme, Policy = AdminPolicies.Viewer)]
public class DashboardController : ControllerBase
{
    private static readonly TimeSpan MaximumRange = TimeSpan.FromHours(24);

    private readonly IServerProvider _serverProvider;
    private readonly ServerMetricsSampler _sampler;
    private readonly IServerStatisticsProvider? _statisticsProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="DashboardController"/> class.
    /// </summary>
    /// <param name="serverProvider">The server provider.</param>
    /// <param name="sampler">The metrics sampler.</param>
    /// <param name="statisticsProvider">The statistics provider, which is only available with a real database.</param>
    public DashboardController(IServerProvider serverProvider, ServerMetricsSampler sampler, IServerStatisticsProvider? statisticsProvider = null)
    {
        this._serverProvider = serverProvider;
        this._sampler = sampler;
        this._statisticsProvider = statisticsProvider;
    }

    /// <summary>
    /// Gets the current numbers of the server.
    /// </summary>
    /// <returns>The summary.</returns>
    [HttpGet]
    public async Task<DashboardSummary> GetSummaryAsync()
    {
        var gameServers = this._serverProvider.Servers.ToList().Where(s => s.Type == ServerType.GameServer).ToList();
        var statistics = this._statisticsProvider is null
            ? null
            : await this._statisticsProvider.GetStatisticsAsync(this.HttpContext.RequestAborted).ConfigureAwait(false);
        var latest = this._sampler.Latest;
        return new DashboardSummary(
            this._sampler.GetOnlinePlayers(),
            gameServers.Sum(s => s.MaximumConnections),
            statistics?.AccountCount,
            statistics?.CharacterCount,
            ServerMetricsSampler.ProcessStartTime,
            (long)(DateTime.UtcNow - ServerMetricsSampler.ProcessStartTime).TotalSeconds,
            latest?.CpuPercent,
            latest?.MemoryBytes,
            latest?.MemoryPercent);
    }

    /// <summary>
    /// Gets the sampled metrics of the specified time range.
    /// </summary>
    /// <param name="hours">The number of hours of the range, at most 24.</param>
    /// <returns>The samples, oldest first.</returns>
    [HttpGet("history")]
    public IReadOnlyList<MetricSample> GetHistory([FromQuery] int hours = 24)
    {
        var range = TimeSpan.FromHours(Math.Clamp(hours, 1, (int)MaximumRange.TotalHours));
        return this._sampler.GetSamples(DateTime.UtcNow - range);
    }

    /// <summary>
    /// The current numbers of the server.
    /// </summary>
    /// <param name="OnlinePlayers">The number of online players on all game servers.</param>
    /// <param name="MaximumPlayers">The maximum number of players on all game servers.</param>
    /// <param name="AccountCount">The number of accounts, if available.</param>
    /// <param name="CharacterCount">The number of characters, if available.</param>
    /// <param name="StartedAt">The time when the process was started (UTC).</param>
    /// <param name="UptimeSeconds">The uptime of the process in seconds.</param>
    /// <param name="CpuPercent">The CPU usage of the last sample, in percent.</param>
    /// <param name="MemoryBytes">The working set of the last sample, in bytes.</param>
    /// <param name="MemoryPercent">The memory usage of the last sample, in percent.</param>
    public record DashboardSummary(
        int OnlinePlayers,
        int MaximumPlayers,
        int? AccountCount,
        int? CharacterCount,
        DateTime StartedAt,
        long UptimeSeconds,
        double? CpuPercent,
        long? MemoryBytes,
        double? MemoryPercent);
}
