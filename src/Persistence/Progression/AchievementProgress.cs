// <copyright file="AchievementProgress.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Progression;

/// <summary>
/// The progress of a character or an account in one achievement.
/// </summary>
public class AchievementProgress
{
    /// <summary>
    /// Gets or sets the identifier of the owner: the character, or the account for achievements of the whole account.
    /// </summary>
    public Guid OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the achievement, as configured in the plugin configuration.
    /// </summary>
    public string AchievementId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the identifier of the account of the owner.
    /// </summary>
    public Guid? AccountId { get; set; }

    /// <summary>
    /// Gets or sets the achieved count towards the objective.
    /// </summary>
    public long Count { get; set; }

    /// <summary>
    /// Gets or sets the timestamp (UTC) when the objective has been reached.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp (UTC) when the rewards have been handed out.
    /// </summary>
    /// <remarks>
    /// It stays <c>null</c> as long as a completed achievement couldn't be rewarded yet, e.g. because the inventory was full.
    /// </remarks>
    public DateTime? RewardedAt { get; set; }

    /// <summary>
    /// Creates a copy of this instance.
    /// </summary>
    /// <returns>The copy.</returns>
    public AchievementProgress Clone() => new()
    {
        OwnerId = this.OwnerId,
        AchievementId = this.AchievementId,
        AccountId = this.AccountId,
        Count = this.Count,
        CompletedAt = this.CompletedAt,
        RewardedAt = this.RewardedAt,
    };
}
