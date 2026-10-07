// <copyright file="SeasonPassOverview.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.SeasonPass;

/// <summary>
/// The season pass of an account: the running season, the reached level and the rewards.
/// </summary>
/// <param name="Season">The running season; <c>null</c>, if no season is running.</param>
/// <param name="Experience">The experience of the pass.</param>
/// <param name="Level">The reached level.</param>
/// <param name="IsPremium">If set to <c>true</c>, the premium track is active.</param>
/// <param name="EndUtc">The end of the season (UTC).</param>
/// <param name="Levels">The levels with their rewards, ordered by level.</param>
public sealed record SeasonPassOverview(
    SeasonDefinition? Season,
    long Experience,
    int Level,
    bool IsPremium,
    DateTime EndUtc,
    IReadOnlyList<SeasonPassLevelEntry> Levels)
{
    /// <summary>
    /// Gets an overview without a running season.
    /// </summary>
    public static SeasonPassOverview None { get; } = new(null, 0, 0, false, DateTime.MinValue, []);

    /// <summary>
    /// Gets the number of levels whose rewards can be claimed now.
    /// </summary>
    public int ClaimableCount => this.Levels.Count(l =>
        l.Level.Level <= this.Level
        && ((!l.IsFreeClaimed && l.Level.FreeRewards.Count > 0) || (this.IsPremium && !l.IsPremiumClaimed && l.Level.PremiumRewards.Count > 0)));
}
