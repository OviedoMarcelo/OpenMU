// <copyright file="SeasonPassPlayerState.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.SeasonPass;

using Nito.AsyncEx;

/// <summary>
/// The season pass of the account of a player, which is kept in memory while the player is in the game.
/// </summary>
internal sealed class SeasonPassPlayerState
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SeasonPassPlayerState"/> class.
    /// </summary>
    /// <param name="owner">The plugin which tracks the pass.</param>
    /// <param name="accountId">The identifier of the account.</param>
    /// <param name="characterId">The identifier of the character.</param>
    public SeasonPassPlayerState(SeasonPassPlugIn owner, Guid accountId, Guid characterId)
    {
        this.Owner = owner;
        this.AccountId = accountId;
        this.CharacterId = characterId;
    }

    /// <summary>
    /// Gets the plugin which tracks the pass.
    /// </summary>
    public SeasonPassPlugIn Owner { get; }

    /// <summary>
    /// Gets the lock which has to be held while accessing the state.
    /// </summary>
    public AsyncLock Lock { get; } = new();

    /// <summary>
    /// Gets the identifier of the account.
    /// </summary>
    public Guid AccountId { get; }

    /// <summary>
    /// Gets the identifier of the character.
    /// </summary>
    public Guid CharacterId { get; }

    /// <summary>
    /// Gets or sets the identifier of the season whose state is loaded; <c>null</c>, if none is loaded.
    /// </summary>
    public string? SeasonId { get; set; }

    /// <summary>
    /// Gets or sets the experience of the pass, including the <see cref="PendingExperience"/>.
    /// </summary>
    public long Experience { get; set; }

    /// <summary>
    /// Gets or sets the experience which hasn't been saved yet.
    /// </summary>
    public long PendingExperience { get; set; }

    /// <summary>
    /// Gets or sets the killed monsters which didn't give experience of the pass yet.
    /// They're only kept in memory.
    /// </summary>
    public int KillCount { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the premium track is active.
    /// </summary>
    public bool IsPremium { get; set; }

    /// <summary>
    /// Gets the rewards which have been handed out, by level and track.
    /// </summary>
    public HashSet<(int Level, bool IsPremium)> Claims { get; } = new();
}
