// <copyright file="AchievementPlayerState.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.Achievements;

using MUnique.OpenMU.Persistence.Progression;
using Nito.AsyncEx;

/// <summary>
/// The achievements and titles of a player, which are kept in memory while the player is in the game.
/// </summary>
internal sealed class AchievementPlayerState
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AchievementPlayerState"/> class.
    /// </summary>
    /// <param name="owner">The plugin which tracks the progress.</param>
    /// <param name="characterId">The identifier of the character.</param>
    /// <param name="accountId">The identifier of the account of the character.</param>
    public AchievementPlayerState(AchievementsPlugIn owner, Guid characterId, Guid? accountId)
    {
        this.Owner = owner;
        this.CharacterId = characterId;
        this.AccountId = accountId;
    }

    /// <summary>
    /// Gets the plugin which tracks the progress.
    /// </summary>
    public AchievementsPlugIn Owner { get; }

    /// <summary>
    /// Gets the lock which has to be held while accessing the state.
    /// </summary>
    public AsyncLock Lock { get; } = new();

    /// <summary>
    /// Gets the identifier of the character.
    /// </summary>
    public Guid CharacterId { get; }

    /// <summary>
    /// Gets the identifier of the account of the character.
    /// </summary>
    public Guid? AccountId { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the progress has been loaded.
    /// </summary>
    public bool IsLoaded { get; set; }

    /// <summary>
    /// Gets the progress by achievement id. It holds the entries of the character and of its account.
    /// </summary>
    public Dictionary<string, AchievementProgress> Progress { get; } = new();

    /// <summary>
    /// Gets the ids of the achievements whose progress has to be saved.
    /// </summary>
    public HashSet<string> DirtyAchievementIds { get; } = new();

    /// <summary>
    /// Gets the ids of the titles which the character and its account unlocked.
    /// </summary>
    public HashSet<string> UnlockedTitleIds { get; } = new();

    /// <summary>
    /// Gets or sets the id of the title which the character shows.
    /// </summary>
    public string? ActiveTitleId { get; set; }

    /// <summary>
    /// Gets the identifier of the owner of the progress of an achievement.
    /// </summary>
    /// <param name="scope">The scope of the achievement.</param>
    /// <returns>The identifier of the account for account achievements, otherwise the one of the character.</returns>
    public Guid GetOwnerId(AchievementScope scope)
    {
        return scope == AchievementScope.Account && this.AccountId is { } accountId ? accountId : this.CharacterId;
    }

    /// <summary>
    /// Gets the progress of an achievement, and creates it if there is none yet.
    /// </summary>
    /// <param name="achievement">The achievement.</param>
    /// <returns>The progress.</returns>
    public AchievementProgress GetOrCreateProgress(AchievementDefinition achievement)
    {
        if (!this.Progress.TryGetValue(achievement.Id, out var progress))
        {
            progress = new AchievementProgress
            {
                OwnerId = this.GetOwnerId(achievement.Scope),
                AchievementId = achievement.Id,
                AccountId = this.AccountId,
            };
            this.Progress.Add(achievement.Id, progress);
        }

        return progress;
    }
}
