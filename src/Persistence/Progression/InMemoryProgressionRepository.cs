// <copyright file="InMemoryProgressionRepository.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Progression;

using System.Collections.Concurrent;
using System.Threading;

/// <summary>
/// A <see cref="IProgressionRepository"/> which keeps everything only in memory.
/// It's used when no database backed repository is registered, e.g. for the demo mode and tests.
/// </summary>
public class InMemoryProgressionRepository : IProgressionRepository
{
    private readonly ConcurrentDictionary<(Guid OwnerId, string AchievementId), AchievementProgress> _achievements = new();
    private readonly ConcurrentDictionary<(Guid OwnerId, string TitleId), UnlockedTitle> _titles = new();
    private readonly ConcurrentDictionary<Guid, string> _activeTitles = new();

    /// <inheritdoc />
    public ValueTask<IList<AchievementProgress>> LoadAchievementsAsync(IReadOnlyCollection<Guid> ownerIds, CancellationToken cancellationToken = default)
    {
        IList<AchievementProgress> result = this._achievements.Values
            .Where(e => ownerIds.Contains(e.OwnerId))
            .Select(e => e.Clone())
            .ToList();
        return ValueTask.FromResult(result);
    }

    /// <inheritdoc />
    public ValueTask SaveAchievementsAsync(IEnumerable<AchievementProgress> progress, CancellationToken cancellationToken = default)
    {
        foreach (var entry in progress)
        {
            this._achievements[(entry.OwnerId, entry.AchievementId)] = entry.Clone();
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<IList<UnlockedTitle>> LoadUnlockedTitlesAsync(IReadOnlyCollection<Guid> ownerIds, CancellationToken cancellationToken = default)
    {
        IList<UnlockedTitle> result = this._titles.Values
            .Where(t => ownerIds.Contains(t.OwnerId))
            .Select(t => new UnlockedTitle { OwnerId = t.OwnerId, TitleId = t.TitleId, UnlockedAt = t.UnlockedAt, Source = t.Source })
            .ToList();
        return ValueTask.FromResult(result);
    }

    /// <inheritdoc />
    public ValueTask<bool> AddUnlockedTitleAsync(UnlockedTitle title, CancellationToken cancellationToken = default)
    {
        var copy = new UnlockedTitle { OwnerId = title.OwnerId, TitleId = title.TitleId, UnlockedAt = title.UnlockedAt, Source = title.Source };
        return ValueTask.FromResult(this._titles.TryAdd((title.OwnerId, title.TitleId), copy));
    }

    /// <inheritdoc />
    public ValueTask<bool> RemoveUnlockedTitleAsync(IReadOnlyCollection<Guid> ownerIds, string titleId, CancellationToken cancellationToken = default)
    {
        var removed = false;
        foreach (var ownerId in ownerIds)
        {
            removed |= this._titles.TryRemove((ownerId, titleId), out _);
        }

        return ValueTask.FromResult(removed);
    }

    /// <inheritdoc />
    public ValueTask<IList<ActiveTitle>> LoadActiveTitlesAsync(IReadOnlyCollection<Guid> characterIds, CancellationToken cancellationToken = default)
    {
        var result = new List<ActiveTitle>();
        foreach (var id in characterIds)
        {
            if (this._activeTitles.TryGetValue(id, out var titleId))
            {
                result.Add(new ActiveTitle { CharacterId = id, TitleId = titleId });
            }
        }

        return ValueTask.FromResult<IList<ActiveTitle>>(result);
    }

    /// <inheritdoc />
    public ValueTask SetActiveTitleAsync(Guid characterId, string? titleId, CancellationToken cancellationToken = default)
    {
        if (titleId is null)
        {
            this._activeTitles.TryRemove(characterId, out _);
        }
        else
        {
            this._activeTitles[characterId] = titleId;
        }

        return ValueTask.CompletedTask;
    }
}
