// <copyright file="MetricSample.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AdminApi;

/// <summary>
/// A sample of the metrics of the server.
/// </summary>
/// <param name="Timestamp">The time when the sample was taken (UTC).</param>
/// <param name="OnlinePlayers">The number of online players.</param>
/// <param name="CpuPercent">The CPU usage of this process, relative to all cores, in percent.</param>
/// <param name="MemoryBytes">The working set of this process, in bytes.</param>
/// <param name="MemoryPercent">The working set relative to the available memory, in percent.</param>
public record MetricSample(DateTime Timestamp, int OnlinePlayers, double CpuPercent, long MemoryBytes, double MemoryPercent);
