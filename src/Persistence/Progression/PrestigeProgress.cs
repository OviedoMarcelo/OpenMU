// <copyright file="PrestigeProgress.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Progression;

/// <summary>
/// The prestige of a character: how often it started its master level over.
/// </summary>
public class PrestigeProgress
{
    /// <summary>
    /// Gets or sets the identifier of the character.
    /// </summary>
    public Guid CharacterId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the account of the character.
    /// </summary>
    public Guid? AccountId { get; set; }

    /// <summary>
    /// Gets or sets the prestige level.
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    /// Gets or sets the prestige points, which can be spent later.
    /// </summary>
    public int Points { get; set; }

    /// <summary>
    /// Gets or sets the timestamp (UTC) of the last prestige.
    /// </summary>
    public DateTime? LastPrestigeAt { get; set; }
}
