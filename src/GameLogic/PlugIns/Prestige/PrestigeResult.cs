// <copyright file="PrestigeResult.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.Prestige;

/// <summary>
/// The result of a prestige attempt.
/// </summary>
public enum PrestigeResult
{
    /// <summary>
    /// The prestige has been done.
    /// </summary>
    Done,

    /// <summary>
    /// The character doesn't have the required level.
    /// </summary>
    LevelTooLow,

    /// <summary>
    /// The character doesn't have the required master level.
    /// </summary>
    MasterLevelTooLow,

    /// <summary>
    /// The character reached the highest prestige level.
    /// </summary>
    MaximumReached,

    /// <summary>
    /// The rewards of the next prestige level don't fit into the inventory.
    /// </summary>
    RewardsDontFit,

    /// <summary>
    /// The prestige isn't available, e.g. because the storage can't be reached or the character isn't in the game.
    /// </summary>
    NotAvailable,
}
