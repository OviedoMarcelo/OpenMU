// <copyright file="CosmeticSetPlayerState.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.CosmeticSets;

using Nito.AsyncEx;

/// <summary>
/// The cosmetic sets of a player, which are kept in memory while the player is in the game.
/// </summary>
internal sealed class CosmeticSetPlayerState
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CosmeticSetPlayerState"/> class.
    /// </summary>
    /// <param name="characterId">The identifier of the character.</param>
    public CosmeticSetPlayerState(Guid characterId)
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
    /// Gets or sets a value indicating whether the sets have been loaded.
    /// </summary>
    public bool IsLoaded { get; set; }

    /// <summary>
    /// Gets the numbers of the sets which the character unlocked.
    /// </summary>
    public HashSet<short> UnlockedSetNumbers { get; } = new();

    /// <summary>
    /// Gets or sets the number of the set which the character wears.
    /// </summary>
    public short? ActiveSetNumber { get; set; }
}
