// <copyright file="SeasonPassBossEncounterExperience.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.SeasonPass;

using MUnique.OpenMU.DataModel.Composition;
using MUnique.OpenMU.GameLogic.PlugIns.BossEncounters;

/// <summary>
/// The experience of the season pass for taking part in a boss encounter in which the boss is defeated.
/// </summary>
public class SeasonPassBossEncounterExperience
{
    /// <summary>
    /// Gets or sets the name of the boss encounter. Empty means any encounter which has no entry of its own.
    /// </summary>
    [Display(Name = "Encuentro de jefe", Description = "Un encuentro del plugin de boss encounters. Vacío = cualquier encuentro que no tenga su propia entrada.")]
    [KeyOf(nameof(BossEncounterConfiguration.Encounters), PlugIn = typeof(BossEncounterPlugIn), KeyProperty = nameof(BossEncounterDefinition.Name), CaptionProperty = nameof(BossEncounterDefinition.Name))]
    public string? EncounterName { get; set; }

    /// <summary>
    /// Gets or sets the experience for each participant, when the boss is defeated.
    /// </summary>
    [Display(Name = "Puntos por participar", Description = "Puntos del pase para cada jugador que participó con el daño (o la curación) mínimo del encuentro, si el jefe muere. No cuentan para el tope semanal.")]
    [Range(0, int.MaxValue)]
    public int Experience { get; set; }

    /// <inheritdoc />
    public override string ToString() => string.IsNullOrWhiteSpace(this.EncounterName) ? "*" : this.EncounterName;
}
