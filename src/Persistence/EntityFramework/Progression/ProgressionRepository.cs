// <copyright file="ProgressionRepository.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework.Progression;

using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.Persistence.Progression;
using Nito.AsyncEx;

/// <summary>
/// Implementation of the <see cref="IProgressionRepository"/> which stores the progression
/// in the <c>progression</c> schema of the configured PostgreSQL database.
/// </summary>
public sealed class ProgressionRepository : IProgressionRepository, IDisposable
{
    /// <summary>
    /// The time to wait before the storage is probed again after a failed attempt.
    /// </summary>
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(30);

    private readonly ILogger<ProgressionRepository> _logger;
    private readonly SetupService? _setupService;
    private readonly AsyncLock _storageLock = new();
    private bool _isStorageReady;
    private DateTime _nextProbeAt = DateTime.MinValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProgressionRepository"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="setupService">The setup service, which notifies about a re-created database.</param>
    public ProgressionRepository(ILogger<ProgressionRepository> logger, SetupService? setupService = null)
    {
        this._logger = logger;
        this._setupService = setupService;
        if (this._setupService is not null)
        {
            this._setupService.DatabaseInitialized += this.OnDatabaseInitializedAsync;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (this._setupService is not null)
        {
            this._setupService.DatabaseInitialized -= this.OnDatabaseInitializedAsync;
        }
    }

    /// <inheritdoc />
    public async ValueTask<IList<AchievementProgress>> LoadAchievementsAsync(IReadOnlyCollection<Guid> ownerIds, CancellationToken cancellationToken = default)
    {
        await this.EnsureAvailableStorageAsync(cancellationToken).ConfigureAwait(false);

        var ids = ownerIds.ToArray();
        await using var context = new ProgressionContext();
        return await context.Achievements
            .AsNoTracking()
            .Where(p => ids.Contains(p.OwnerId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask SaveAchievementsAsync(IEnumerable<AchievementProgress> progress, CancellationToken cancellationToken = default)
    {
        var entries = progress.ToList();
        if (entries.Count == 0)
        {
            return;
        }

        await this.EnsureAvailableStorageAsync(cancellationToken).ConfigureAwait(false);

        await using var context = new ProgressionContext();
        foreach (var group in entries.GroupBy(e => e.OwnerId))
        {
            var achievementIds = group.Select(e => e.AchievementId).ToList();
            var existing = await context.Achievements
                .Where(p => p.OwnerId == group.Key && achievementIds.Contains(p.AchievementId))
                .ToDictionaryAsync(p => p.AchievementId, cancellationToken)
                .ConfigureAwait(false);

            foreach (var entry in group)
            {
                if (existing.TryGetValue(entry.AchievementId, out var stored))
                {
                    stored.AccountId ??= entry.AccountId;
                    stored.Count = entry.Count;
                    stored.CompletedAt = entry.CompletedAt;
                    stored.RewardedAt = entry.RewardedAt;
                }
                else
                {
                    context.Achievements.Add(entry.Clone());
                }
            }
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<IList<UnlockedTitle>> LoadUnlockedTitlesAsync(IReadOnlyCollection<Guid> ownerIds, CancellationToken cancellationToken = default)
    {
        await this.EnsureAvailableStorageAsync(cancellationToken).ConfigureAwait(false);

        var ids = ownerIds.ToArray();
        await using var context = new ProgressionContext();
        return await context.UnlockedTitles
            .AsNoTracking()
            .Where(t => ids.Contains(t.OwnerId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<bool> AddUnlockedTitleAsync(UnlockedTitle title, CancellationToken cancellationToken = default)
    {
        await this.EnsureAvailableStorageAsync(cancellationToken).ConfigureAwait(false);

        await using var context = new ProgressionContext();

        // Two game servers may unlock the same account title at the same time, so the database decides.
        var added = await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO progression."UnlockedTitle" ("OwnerId", "TitleId", "UnlockedAt", "Source")
            VALUES ({title.OwnerId}, {title.TitleId}, {title.UnlockedAt}, {title.Source})
            ON CONFLICT DO NOTHING
            """,
            cancellationToken).ConfigureAwait(false);
        return added > 0;
    }

    /// <inheritdoc />
    public async ValueTask<bool> RemoveUnlockedTitleAsync(IReadOnlyCollection<Guid> ownerIds, string titleId, CancellationToken cancellationToken = default)
    {
        await this.EnsureAvailableStorageAsync(cancellationToken).ConfigureAwait(false);

        var ids = ownerIds.ToArray();
        await using var context = new ProgressionContext();
        var removed = await context.UnlockedTitles
            .Where(t => ids.Contains(t.OwnerId) && t.TitleId == titleId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
        return removed > 0;
    }

    /// <inheritdoc />
    public async ValueTask<IList<ActiveTitle>> LoadActiveTitlesAsync(IReadOnlyCollection<Guid> characterIds, CancellationToken cancellationToken = default)
    {
        await this.EnsureAvailableStorageAsync(cancellationToken).ConfigureAwait(false);

        var ids = characterIds.ToArray();
        await using var context = new ProgressionContext();
        return await context.ActiveTitles
            .AsNoTracking()
            .Where(t => ids.Contains(t.CharacterId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask SetActiveTitleAsync(Guid characterId, string? titleId, CancellationToken cancellationToken = default)
    {
        await this.EnsureAvailableStorageAsync(cancellationToken).ConfigureAwait(false);

        await using var context = new ProgressionContext();
        if (titleId is null)
        {
            await context.ActiveTitles
                .Where(t => t.CharacterId == characterId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO progression."ActiveTitle" ("CharacterId", "TitleId")
            VALUES ({characterId}, {titleId})
            ON CONFLICT ("CharacterId") DO UPDATE SET "TitleId" = EXCLUDED."TitleId"
            """,
            cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask EnsureAvailableStorageAsync(CancellationToken cancellationToken)
    {
        if (this._isStorageReady)
        {
            return;
        }

        if (DateTime.UtcNow < this._nextProbeAt)
        {
            throw new InvalidOperationException("The progression storage is not available. Please check the database connection.");
        }

        using var l = await this._storageLock.LockAsync(cancellationToken).ConfigureAwait(false);
        if (this._isStorageReady)
        {
            return;
        }

        try
        {
            await using var context = new ProgressionContext();
            await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            this._isStorageReady = true;
        }
        catch (Exception ex)
        {
            this._nextProbeAt = DateTime.UtcNow + RetryDelay;
            this._logger.LogWarning(ex, "The progression storage is not available (yet).");
            throw new InvalidOperationException("The progression storage is not available. Please check the database connection.", ex);
        }
    }

    private ValueTask OnDatabaseInitializedAsync()
    {
        this._isStorageReady = false;
        return ValueTask.CompletedTask;
    }
}
