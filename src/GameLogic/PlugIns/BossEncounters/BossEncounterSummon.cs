// <copyright file="BossEncounterSummon.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.BossEncounters;

/// <summary>
/// Monsters which the boss summons around itself during a phase.
/// </summary>
public class BossEncounterSummon
{
    /// <summary>
    /// Gets or sets the number of the summoned monster.
    /// </summary>
    [Display(Name = "Número de monstruo")]
    public short MonsterNumber { get; set; }

    /// <summary>
    /// Gets or sets the number of monsters which are summoned each time.
    /// </summary>
    [Display(Name = "Cantidad")]
    [Range(1, 50)]
    public int Quantity { get; set; } = 1;

    /// <inheritdoc />
    public override string ToString() => $"{this.Quantity} x {this.MonsterNumber}";
}
