// <copyright file="SeasonLevelDefinition.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.SeasonPass;

using System.Globalization;
using MUnique.OpenMU.DataModel.Composition;
using MUnique.OpenMU.GameLogic.PlugIns.WeeklyQuests;

/// <summary>
/// A level of the season pass with its rewards.
/// </summary>
public class SeasonLevelDefinition
{
    /// <summary>
    /// Gets or sets the level, starting at 1.
    /// </summary>
    [Display(Name = "Nivel", Description = "Nivel del pase en el que se gana el premio, empezando por 1. El nivel más alto configurado es el máximo del pase.")]
    [Range(1, 100)]
    public int Level { get; set; } = 1;

    /// <summary>
    /// Gets or sets the rewards of the free track, which every account gets.
    /// </summary>
    [Display(Name = "Premios gratis", Description = "Para todas las cuentas.")]
    [MemberOfAggregate]
    public ICollection<WeeklyQuestReward> FreeRewards { get; set; } = new List<WeeklyQuestReward>();

    /// <summary>
    /// Gets or sets the rewards of the premium track, which only accounts with the premium pass get.
    /// </summary>
    [Display(Name = "Premios premium", Description = "Solo para las cuentas con el pase premium.")]
    [MemberOfAggregate]
    public ICollection<WeeklyQuestReward> PremiumRewards { get; set; } = new List<WeeklyQuestReward>();

    /// <inheritdoc />
    public override string ToString() => this.Level.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Gets the rewards of a track as text.
    /// </summary>
    /// <param name="isPremium">If set to <c>true</c>, of the premium track; otherwise of the free one.</param>
    /// <param name="culture">The culture.</param>
    /// <returns>The rewards, separated by comma; empty, if there are none.</returns>
    public string GetRewardsText(bool isPremium, CultureInfo culture)
    {
        var rewards = isPremium ? this.PremiumRewards : this.FreeRewards;
        return string.Join(", ", rewards.Select(r => r.GetDisplayText(culture)));
    }
}
