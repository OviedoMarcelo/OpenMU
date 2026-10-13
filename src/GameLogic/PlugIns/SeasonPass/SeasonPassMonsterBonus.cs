// <copyright file="SeasonPassMonsterBonus.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.SeasonPass;

/// <summary>
/// The experience of the season pass for killing a specific monster, e.g. a boss or a monster of an invasion.
/// </summary>
public class SeasonPassMonsterBonus
{
    /// <summary>
    /// Gets or sets the monster.
    /// </summary>
    [Display(Name = "Monstruo")]
    public virtual MonsterDefinition? Monster { get; set; }

    /// <summary>
    /// Gets or sets the experience for each kill of the monster.
    /// </summary>
    [Display(Name = "XP por kill", Description = "XP del pase por cada kill de este monstruo. Cuenta para el tope semanal.")]
    [Range(0, int.MaxValue)]
    public int Experience { get; set; }

    /// <inheritdoc />
    public override string ToString() => this.Monster?.ToString() ?? string.Empty;
}
