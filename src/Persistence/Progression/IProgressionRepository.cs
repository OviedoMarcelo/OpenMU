// <copyright file="IProgressionRepository.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Progression;

using System.Threading;

/// <summary>
/// A repository for the long term progression of characters and accounts: achievements and titles.
/// </summary>
public interface IProgressionRepository
{
    /// <summary>
    /// Loads the achievement progress of the specified owners, with one query.
    /// </summary>
    /// <param name="ownerIds">The identifiers of the owners, e.g. of a character and its account.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The stored progress of the owners.</returns>
    ValueTask<IList<AchievementProgress>> LoadAchievementsAsync(IReadOnlyCollection<Guid> ownerIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts or updates the specified achievement progress entries.
    /// </summary>
    /// <param name="progress">The progress entries.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    ValueTask SaveAchievementsAsync(IEnumerable<AchievementProgress> progress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the unlocked titles of the specified owners, with one query.
    /// </summary>
    /// <param name="ownerIds">The identifiers of the owners, e.g. of a character and its account.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The unlocked titles of the owners.</returns>
    ValueTask<IList<UnlockedTitle>> LoadUnlockedTitlesAsync(IReadOnlyCollection<Guid> ownerIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds an unlocked title, if the owner doesn't have it yet.
    /// </summary>
    /// <param name="title">The unlocked title.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><c>true</c>, if the title has been added; <c>false</c>, if the owner already had it.</returns>
    ValueTask<bool> AddUnlockedTitleAsync(UnlockedTitle title, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes an unlocked title of the specified owners, e.g. of a character and its account.
    /// </summary>
    /// <param name="ownerIds">The identifiers of the owners.</param>
    /// <param name="titleId">The identifier of the title.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><c>true</c>, if a title has been removed.</returns>
    ValueTask<bool> RemoveUnlockedTitleAsync(IReadOnlyCollection<Guid> ownerIds, string titleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the active titles of the specified characters, with one query.
    /// </summary>
    /// <param name="characterIds">The identifiers of the characters.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The active titles of the characters which show one.</returns>
    ValueTask<IList<ActiveTitle>> LoadActiveTitlesAsync(IReadOnlyCollection<Guid> characterIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the title which a character shows below its name.
    /// </summary>
    /// <param name="characterId">The identifier of the character.</param>
    /// <param name="titleId">The identifier of the title; <c>null</c> to show none.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    ValueTask SetActiveTitleAsync(Guid characterId, string? titleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the state of the pass of a season for an account, with one round trip per table.
    /// </summary>
    /// <param name="accountId">The identifier of the account.</param>
    /// <param name="seasonId">The identifier of the season.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The state; an empty one, if the account has none yet.</returns>
    ValueTask<SeasonPassState> LoadSeasonPassAsync(Guid accountId, string seasonId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds experience to the pass of a season of an account.
    /// </summary>
    /// <remarks>
    /// The experience is added in the database, so that it can't get lost when two servers add some at the same time.
    /// </remarks>
    /// <param name="accountId">The identifier of the account.</param>
    /// <param name="seasonId">The identifier of the season.</param>
    /// <param name="experience">The experience which is added.</param>
    /// <param name="cappedExperience">The part of <paramref name="experience"/> which came from sources with a weekly limit.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The experience of the pass after adding it.</returns>
    ValueTask<long> AddSeasonExperienceAsync(Guid accountId, string seasonId, long experience, long cappedExperience = 0, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that a reward of the pass has been handed out, if it hasn't been yet.
    /// </summary>
    /// <param name="claim">The claim.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><c>true</c>, if the claim has been added; <c>false</c>, if the reward was already handed out.</returns>
    ValueTask<bool> AddSeasonClaimAsync(SeasonClaim claim, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a claim again, e.g. when the reward couldn't be handed out after all.
    /// </summary>
    /// <param name="claim">The claim.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    ValueTask RemoveSeasonClaimAsync(SeasonClaim claim, CancellationToken cancellationToken = default);

    /// <summary>
    /// Activates the premium track of the pass of a season for an account.
    /// </summary>
    /// <param name="premium">The activation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><c>true</c>, if it has been activated; <c>false</c>, if it already was.</returns>
    ValueTask<bool> AddSeasonPremiumAsync(SeasonPremium premium, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the prestige of the specified characters, with one query.
    /// </summary>
    /// <param name="characterIds">The identifiers of the characters.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The prestige of the characters which have one.</returns>
    ValueTask<IList<PrestigeProgress>> LoadPrestigeAsync(IReadOnlyCollection<Guid> characterIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts or updates the prestige of a character.
    /// </summary>
    /// <param name="prestige">The prestige.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    ValueTask SavePrestigeAsync(PrestigeProgress prestige, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the unlocked cosmetic pieces of a character.
    /// </summary>
    /// <param name="characterId">The identifier of the character.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The unlocked cosmetic pieces of the character.</returns>
    ValueTask<IList<UnlockedCosmeticPiece>> LoadUnlockedCosmeticPiecesAsync(Guid characterId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds an unlocked cosmetic piece, if the character doesn't have it yet.
    /// </summary>
    /// <param name="piece">The unlocked piece.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><c>true</c>, if the piece has been added; <c>false</c>, if the character already had it.</returns>
    ValueTask<bool> AddUnlockedCosmeticPieceAsync(UnlockedCosmeticPiece piece, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes an unlocked cosmetic piece of a character, e.g. when it was given by mistake.
    /// If the character wears it, it's taken off, too.
    /// </summary>
    /// <param name="characterId">The identifier of the character.</param>
    /// <param name="pieceNumber">The number of the piece.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><c>true</c>, if a piece has been removed.</returns>
    ValueTask<bool> RemoveUnlockedCosmeticPieceAsync(Guid characterId, short pieceNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the cosmetic pieces which a character wears.
    /// </summary>
    /// <param name="characterId">The identifier of the character.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The worn pieces, at most one per item slot.</returns>
    ValueTask<IList<WornCosmeticPiece>> LoadWornCosmeticPiecesAsync(Guid characterId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the cosmetic piece which a character wears over the item of a slot.
    /// </summary>
    /// <param name="characterId">The identifier of the character.</param>
    /// <param name="itemSlot">The item slot.</param>
    /// <param name="pieceNumber">The number of the piece; <c>null</c> to wear none in the slot.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    ValueTask SetWornCosmeticPieceAsync(Guid characterId, byte itemSlot, short? pieceNumber, CancellationToken cancellationToken = default);
}
