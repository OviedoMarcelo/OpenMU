// <copyright file="BossEncounterRankReward.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.BossEncounters;

using System.Globalization;
using MUnique.OpenMU.DataModel.Composition;

/// <summary>
/// A reward for the players at some places of a ranking of a defeated boss encounter, e.g. a better item for the
/// player with the most damage. It's given in addition to the reward for the participation.
/// </summary>
[HorizontalLayout]
public class BossEncounterRankReward : BossEncounterDrop
{
    /// <summary>
    /// Gets or sets the ranking.
    /// </summary>
    [Display(Name = "Ranking", Order = -3)]
    public BossEncounterRanking Ranking { get; set; }

    /// <summary>
    /// Gets or sets the first place which gets the reward.
    /// </summary>
    [Display(Name = "Desde el puesto", Order = -2)]
    [Range(1, 100)]
    public int FromRank { get; set; } = 1;

    /// <summary>
    /// Gets or sets the last place which gets the reward.
    /// </summary>
    [Display(Name = "Hasta el puesto", Description = "Igual a \"Desde\" para un solo puesto.", Order = -1)]
    [Range(1, 100)]
    public int ToRank { get; set; } = 1;

    /// <summary>
    /// Gets or sets the money which each of the players gets.
    /// </summary>
    [Display(Name = "Zen", Description = "Sin item = solo zen.", Order = 9)]
    [Range(0, int.MaxValue)]
    public int Money { get; set; }

    /// <summary>
    /// Gets or sets the number of the cosmetic piece which each of the players unlocks; 0 for none.
    /// </summary>
    [Display(Name = "Pieza cosmética", Description = "Número de la pieza del plugin Cosmetics que desbloquea cada jugador. 0 = ninguna.", Order = 10)]
    [Range(0, short.MaxValue)]
    public short CosmeticPieceNumber { get; set; }

    /// <summary>
    /// Gets a value indicating whether the place gets this reward.
    /// </summary>
    /// <param name="rank">The place in the ranking, starting with 1.</param>
    /// <returns><c>true</c>, if the place gets this reward.</returns>
    public bool AppliesTo(int rank) => rank >= this.FromRank && rank <= Math.Max(this.FromRank, this.ToRank);

    /// <inheritdoc />
    public override string ToString()
    {
        var places = this.ToRank > this.FromRank ? $"{this.FromRank}-{this.ToRank}" : this.FromRank.ToString(CultureInfo.InvariantCulture);
        return $"{this.Ranking} #{places}: {base.ToString()}";
    }
}
