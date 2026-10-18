// <copyright file="CosmeticChangeResult.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.Cosmetics;

/// <summary>
/// The result of changing the cosmetic pieces which a character wears.
/// </summary>
public enum CosmeticChangeResult
{
    /// <summary>
    /// The piece has been put on.
    /// </summary>
    Worn,

    /// <summary>
    /// The piece or the pieces have been taken off.
    /// </summary>
    TakenOff,

    /// <summary>
    /// The character didn't wear the piece, or none at all.
    /// </summary>
    NotWorn,

    /// <summary>
    /// The piece exists, but the character hasn't unlocked it.
    /// </summary>
    NotUnlocked,

    /// <summary>
    /// There is no piece with the specified number, or the game has no such item.
    /// </summary>
    Unknown,

    /// <summary>
    /// The class of the character can't wear the piece.
    /// </summary>
    NotQualified,

    /// <summary>
    /// The pieces are not available, e.g. because the storage can't be reached.
    /// </summary>
    NotAvailable,
}
