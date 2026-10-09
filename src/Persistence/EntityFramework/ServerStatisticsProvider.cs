// <copyright file="ServerStatisticsProvider.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework;

using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// <summary>
/// Counts the stored accounts and characters directly in the database.
/// </summary>
public sealed class ServerStatisticsProvider : IServerStatisticsProvider
{
    private readonly ILogger<ServerStatisticsProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerStatisticsProvider"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public ServerStatisticsProvider(ILogger<ServerStatisticsProvider> logger)
    {
        this._logger = logger;
    }

    /// <inheritdoc />
    public async ValueTask<ServerStatistics?> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = new EntityDataContext();
            var accounts = await context.Set<Model.Account>().CountAsync(cancellationToken).ConfigureAwait(false);
            var characters = await context.Set<Model.Character>().CountAsync(cancellationToken).ConfigureAwait(false);
            return new ServerStatistics(accounts, characters);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Expected before the database is reachable or initialized.
            this._logger.LogDebug(ex, "The server statistics couldn't be determined.");
            return null;
        }
    }
}
