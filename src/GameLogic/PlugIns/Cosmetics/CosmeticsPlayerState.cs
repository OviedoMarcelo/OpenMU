// <copyright file="CosmeticsPlayerState.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.Cosmetics;

using Nito.AsyncEx;

/// <summary>
/// The cosmetic pieces of a player, which are kept in memory while the player is in the game.
/// </summary>
internal sealed class CosmeticsPlayerState
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CosmeticsPlayerState"/> class.
    /// </summary>
    /// <param name="characterId">The identifier of the character.</param>
    public CosmeticsPlayerState(Guid characterId)
    {
        this.CharacterId = characterId;
    }

    /// <summary>
    /// Gets the lock which has to be held while accessing the state.
    /// </summary>
    public AsyncLock Lock { get; } = new();

    /// <summary>
    /// Gets the identifier of the character.
    /// </summary>
    public Guid CharacterId { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the pieces have been loaded.
    /// </summary>
    public bool IsLoaded { get; set; }

    /// <summary>
    /// Gets the numbers of the pieces which the character unlocked.
    /// </summary>
    public HashSet<short> UnlockedPieceNumbers { get; } = new();

    /// <summary>
    /// Gets the numbers of the pieces which the character wears, by the slot they cover.
    /// </summary>
    public Dictionary<CosmeticSlot, short> WornPieceNumbers { get; } = new();
}
