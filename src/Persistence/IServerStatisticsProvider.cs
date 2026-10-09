// <copyright file="IServerStatisticsProvider.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence;

using System.Threading;

/// <summary>
/// Provides aggregated numbers about the stored data, e.g. for a dashboard.
/// </summary>
public interface IServerStatisticsProvider
{
    /// <summary>
    /// Gets the number of stored accounts and characters.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The numbers, or <c>null</c> when the storage isn't available.</returns>
    ValueTask<ServerStatistics?> GetStatisticsAsync(CancellationToken cancellationToken = default);
}
