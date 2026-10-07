// <copyright file="TitleChangeResult.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.Achievements;

/// <summary>
/// The result of changing the title which a character shows.
/// </summary>
public enum TitleChangeResult
{
    /// <summary>
    /// The title has been changed.
    /// </summary>
    Changed,

    /// <summary>
    /// The title has been removed.
    /// </summary>
    Removed,

    /// <summary>
    /// The title exists, but the character hasn't unlocked it.
    /// </summary>
    NotUnlocked,

    /// <summary>
    /// There is no title with the specified id.
    /// </summary>
    Unknown,

    /// <summary>
    /// The titles are not available, e.g. because the storage can't be reached.
    /// </summary>
    NotAvailable,
}
