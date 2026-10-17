// <copyright file="UnlockedCosmeticSet.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Progression;

/// <summary>
/// A cosmetic set which a character has unlocked.
/// </summary>
public class UnlockedCosmeticSet
{
    /// <summary>
    /// Gets or sets the identifier of the character.
    /// </summary>
    public Guid CharacterId { get; set; }

    /// <summary>
    /// Gets or sets the number of the set, as configured in the plugin configuration.
    /// </summary>
    public short SetNumber { get; set; }

    /// <summary>
    /// Gets or sets the timestamp (UTC) when the set has been unlocked.
    /// </summary>
    public DateTime UnlockedAt { get; set; }

    /// <summary>
    /// Gets or sets where the set came from, e.g. "gm" or the identifier of a quest.
    /// </summary>
    public string? Source { get; set; }
}
