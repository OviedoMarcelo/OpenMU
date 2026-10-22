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
    private readonly ConcurrentDictionary<(Guid AccountId, string SeasonId), (long Experience, long CappedExperience)> _seasonExperience = new();
    private readonly ConcurrentDictionary<(Guid AccountId, string SeasonId, int Level, bool IsPremium), SeasonClaim> _seasonClaims = new();
    private readonly ConcurrentDictionary<(Guid AccountId, string SeasonId), SeasonPremium> _seasonPremiums = new();
    private readonly ConcurrentDictionary<Guid, PrestigeProgress> _prestige = new();
    private readonly ConcurrentDictionary<(Guid CharacterId, short PieceNumber), UnlockedCosmeticPiece> _cosmeticPieces = new();
    private readonly ConcurrentDictionary<(Guid CharacterId, byte ItemSlot), short> _wornCosmeticPieces = new();

    /// <inheritdoc />
    public ValueTask<IList<UnlockedCosmeticPiece>> LoadUnlockedCosmeticPiecesAsync(Guid characterId, CancellationToken cancellationToken = default)
    {
        IList<UnlockedCosmeticPiece> result = this._cosmeticPieces.Values
            .Where(p => p.CharacterId == characterId)
            .Select(Clone)
            .ToList();
        return ValueTask.FromResult(result);
    }

    /// <inheritdoc />
    public ValueTask<bool> AddUnlockedCosmeticPieceAsync(UnlockedCosmeticPiece piece, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(this._cosmeticPieces.TryAdd((piece.CharacterId, piece.PieceNumber), Clone(piece)));
    }

    /// <inheritdoc />
    public ValueTask<bool> RemoveUnlockedCosmeticPieceAsync(Guid characterId, short pieceNumber, CancellationToken cancellationToken = default)
    {
        foreach (var worn in this._wornCosmeticPieces.Where(w => w.Key.CharacterId == characterId && w.Value == pieceNumber).ToList())
        {
            this._wornCosmeticPieces.TryRemove(worn.Key, out _);
        }

        return ValueTask.FromResult(this._cosmeticPieces.TryRemove((characterId, pieceNumber), out _));
    }

    /// <inheritdoc />
    public ValueTask<int> RemoveCosmeticPiecesOfAllCharactersAsync(IReadOnlyCollection<short> pieceNumbers, CancellationToken cancellationToken = default)
    {
        foreach (var worn in this._wornCosmeticPieces.Where(w => pieceNumbers.Contains(w.Value)).ToList())
        {
            this._wornCosmeticPieces.TryRemove(worn.Key, out _);
        }

        var removed = this._cosmeticPieces.Keys.Where(k => pieceNumbers.Contains(k.PieceNumber)).Count(k => this._cosmeticPieces.TryRemove(k, out _));
        return ValueTask.FromResult(removed);
    }

    /// <inheritdoc />
    public ValueTask<IList<WornCosmeticPiece>> LoadWornCosmeticPiecesAsync(Guid characterId, CancellationToken cancellationToken = default)
    {
        IList<WornCosmeticPiece> result = this._wornCosmeticPieces
            .Where(w => w.Key.CharacterId == characterId)
            .Select(w => new WornCosmeticPiece { CharacterId = characterId, ItemSlot = w.Key.ItemSlot, PieceNumber = w.Value })
            .ToList();
        return ValueTask.FromResult(result);
    }

    /// <inheritdoc />
    public ValueTask SetWornCosmeticPieceAsync(Guid characterId, byte itemSlot, short? pieceNumber, CancellationToken cancellationToken = default)
    {
        if (pieceNumber is { } number)
        {
            this._wornCosmeticPieces[(characterId, itemSlot)] = number;
        }
        else
        {
            this._wornCosmeticPieces.TryRemove((characterId, itemSlot), out _);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<IList<PrestigeProgress>> LoadPrestigeAsync(IReadOnlyCollection<Guid> characterIds, CancellationToken cancellationToken = default)
    {
        IList<PrestigeProgress> result = characterIds
            .Select(id => this._prestige.GetValueOrDefault(id))
            .OfType<PrestigeProgress>()
            .Select(Clone)
            .ToList();
        return ValueTask.FromResult(result);
    }

    /// <inheritdoc />
    public ValueTask SavePrestigeAsync(PrestigeProgress prestige, CancellationToken cancellationToken = default)
    {
        this._prestige[prestige.CharacterId] = Clone(prestige);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<SeasonPassState> LoadSeasonPassAsync(Guid accountId, string seasonId, CancellationToken cancellationToken = default)
    {
        var claims = this._seasonClaims.Values
            .Where(c => c.AccountId == accountId && c.SeasonId == seasonId)
            .Select(Clone)
            .ToList();
        var progress = this._seasonExperience.GetValueOrDefault((accountId, seasonId));
        return ValueTask.FromResult(new SeasonPassState(
            progress.Experience,
            claims,
            this._seasonPremiums.ContainsKey((accountId, seasonId)),
            progress.CappedExperience));
    }

    /// <inheritdoc />
    public ValueTask<long> AddSeasonExperienceAsync(Guid accountId, string seasonId, long experience, long cappedExperience = 0, CancellationToken cancellationToken = default)
    {
        var result = this._seasonExperience.AddOrUpdate(
            (accountId, seasonId),
            (experience, cappedExperience),
            (_, current) => (current.Experience + experience, current.CappedExperience + cappedExperience));
        return ValueTask.FromResult(result.Experience);
    }

    /// <inheritdoc />
    public ValueTask<bool> AddSeasonClaimAsync(SeasonClaim claim, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(this._seasonClaims.TryAdd((claim.AccountId, claim.SeasonId, claim.Level, claim.IsPremium), Clone(claim)));
    }

    /// <inheritdoc />
    public ValueTask RemoveSeasonClaimAsync(SeasonClaim claim, CancellationToken cancellationToken = default)
    {
        this._seasonClaims.TryRemove((claim.AccountId, claim.SeasonId, claim.Level, claim.IsPremium), out _);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<bool> AddSeasonPremiumAsync(SeasonPremium premium, CancellationToken cancellationToken = default)
    {
        var copy = new SeasonPremium { AccountId = premium.AccountId, SeasonId = premium.SeasonId, GrantedAt = premium.GrantedAt, GrantedBy = premium.GrantedBy };
        return ValueTask.FromResult(this._seasonPremiums.TryAdd((premium.AccountId, premium.SeasonId), copy));
    }

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

    private static PrestigeProgress Clone(PrestigeProgress prestige) => new()
    {
        CharacterId = prestige.CharacterId,
        AccountId = prestige.AccountId,
        Level = prestige.Level,
        Points = prestige.Points,
        LastPrestigeAt = prestige.LastPrestigeAt,
    };

    private static UnlockedCosmeticPiece Clone(UnlockedCosmeticPiece piece) => new()
    {
        CharacterId = piece.CharacterId,
        PieceNumber = piece.PieceNumber,
        UnlockedAt = piece.UnlockedAt,
        Source = piece.Source,
    };

    private static SeasonClaim Clone(SeasonClaim claim) => new()
    {
        AccountId = claim.AccountId,
        SeasonId = claim.SeasonId,
        Level = claim.Level,
        IsPremium = claim.IsPremium,
        ClaimedAt = claim.ClaimedAt,
        CharacterId = claim.CharacterId,
    };
}
