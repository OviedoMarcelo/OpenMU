// <copyright file="CosmeticSetChangeResult.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.CosmeticSets;

/// <summary>
/// The result of changing the cosmetic set which a character wears.
/// </summary>
public enum CosmeticSetChangeResult
{
    /// <summary>
    /// The set has been changed.
    /// </summary>
    Changed,

    /// <summary>
    /// The character wears no set anymore.
    /// </summary>
    Removed,

    /// <summary>
    /// The set exists, but the character hasn't unlocked it.
    /// </summary>
    NotUnlocked,

    /// <summary>
    /// There is no set with the specified number.
    /// </summary>
    Unknown,

    /// <summary>
    /// The class of the character can't wear the set.
    /// </summary>
    NotQualified,

    /// <summary>
    /// The sets are not available, e.g. because the storage can't be reached.
    /// </summary>
    NotAvailable,
}
