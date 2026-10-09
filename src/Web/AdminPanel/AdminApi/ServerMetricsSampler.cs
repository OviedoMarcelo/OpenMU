// <copyright file="ServerMetricsSampler.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AdminApi;

using System.Diagnostics;
using System.Threading;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.Interfaces;

/// <summary>
/// Samples the number of online players and the load of this process periodically and keeps
/// the samples of the last 24 hours in memory, for the charts of the dashboard.
/// </summary>
/// <remarks>
/// The history is lost when the process restarts - it's meant for a quick overview, not as a
/// replacement of a real monitoring.
/// </remarks>
public sealed class ServerMetricsSampler : BackgroundService
{
    /// <summary>
    /// The interval between two samples.
    /// </summary>
    public static readonly TimeSpan SampleInterval = TimeSpan.FromMinutes(1);

    private static readonly TimeSpan HistoryDuration = TimeSpan.FromHours(24);

    private readonly IServerProvider? _serverProvider;
    private readonly ILogger<ServerMetricsSampler> _logger;
    private readonly Queue<MetricSample> _samples = new();
    private readonly Lock _lock = new();

    private TimeSpan _lastProcessorTime;
    private DateTime _lastSampleTime;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerMetricsSampler"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="serverProvider">The server provider, which is only available when the servers run in this process.</param>
    public ServerMetricsSampler(ILogger<ServerMetricsSampler> logger, IServerProvider? serverProvider = null)
    {
        this._serverProvider = serverProvider;
        this._logger = logger;
        using var process = Process.GetCurrentProcess();
        this._lastProcessorTime = process.TotalProcessorTime;
        this._lastSampleTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets the start time of this process.
    /// </summary>
    public static DateTime ProcessStartTime { get; } = Process.GetCurrentProcess().StartTime.ToUniversalTime();

    /// <summary>
    /// Gets the most recent sample.
    /// </summary>
    public MetricSample? Latest
    {
        get
        {
            lock (this._lock)
            {
                return this._samples.Count > 0 ? this._samples.Last() : null;
            }
        }
    }

    /// <summary>
    /// Gets the samples which were taken since the specified time.
    /// </summary>
    /// <param name="since">The time from which on the samples are returned.</param>
    /// <returns>The samples, oldest first.</returns>
    public IReadOnlyList<MetricSample> GetSamples(DateTime since)
    {
        lock (this._lock)
        {
            return this._samples.Where(s => s.Timestamp >= since).ToList();
        }
    }

    /// <summary>
    /// Gets the number of players which are currently online on all game servers.
    /// </summary>
    /// <returns>The number of online players.</returns>
    public int GetOnlinePlayers()
    {
        return (this._serverProvider?.Servers.ToList() ?? new List<IManageableServer>())
            .Where(s => s.Type == ServerType.GameServer)
            .Sum(s => s.CurrentConnections);
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(SampleInterval);
        do
        {
            try
            {
                this.TakeSample();
            }
            catch (Exception ex)
            {
                this._logger.LogWarning(ex, "The server metrics couldn't be sampled.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    private void TakeSample()
    {
        using var process = Process.GetCurrentProcess();
        var now = DateTime.UtcNow;
        var processorTime = process.TotalProcessorTime;
        var elapsed = now - this._lastSampleTime;
        var cpuPercent = elapsed > TimeSpan.Zero
            ? (processorTime - this._lastProcessorTime).TotalMilliseconds / (elapsed.TotalMilliseconds * Environment.ProcessorCount) * 100
            : 0;
        this._lastProcessorTime = processorTime;
        this._lastSampleTime = now;

        var memoryBytes = process.WorkingSet64;
        var availableMemoryBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        var sample = new MetricSample(
            now,
            this.GetOnlinePlayers(),
            Math.Round(Math.Clamp(cpuPercent, 0, 100), 1),
            memoryBytes,
            availableMemoryBytes > 0 ? Math.Round(Math.Clamp(memoryBytes * 100.0 / availableMemoryBytes, 0, 100), 1) : 0);

        lock (this._lock)
        {
            this._samples.Enqueue(sample);
            while (this._samples.Count > 0 && this._samples.Peek().Timestamp < now - HistoryDuration)
            {
                this._samples.Dequeue();
            }
        }
    }
}
