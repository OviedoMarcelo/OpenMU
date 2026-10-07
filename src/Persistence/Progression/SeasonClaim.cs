// <copyright file="SeasonClaim.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Progression;

/// <summary>
/// A reward of a level of the season pass which an account has received.
/// </summary>
public class SeasonClaim
{
    /// <summary>
    /// Gets or sets the identifier of the account.
    /// </summary>
    public Guid AccountId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the season.
    /// </summary>
    public string SeasonId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the level of the pass.
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether it's the reward of the premium track; otherwise of the free one.
    /// </summary>
    public bool IsPremium { get; set; }

    /// <summary>
    /// Gets or sets the timestamp (UTC) when the reward has been handed out.
    /// </summary>
    public DateTime ClaimedAt { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the character which received the reward.
    /// </summary>
    public Guid? CharacterId { get; set; }
}
