// <copyright file="EconomyConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.Economy;

/// <summary>
/// The configuration of the <see cref="EconomyFeaturePlugIn"/>. With the default values nothing changes.
/// </summary>
public class EconomyConfiguration
{
    /// <summary>
    /// Gets or sets the multiplier of the zen which monsters drop. The dropped zen is the experience of the kill,
    /// so with a high experience rate it's high as well; this multiplier lowers it again, e.g. 0.3.
    /// </summary>
    [Display(Name = "Money drop rate", Description = "Multiplica el zen que tiran los monstruos. El zen es igual a la experiencia del kill, así que con un rate de experiencia alto también sube: con 0.3 cae el 30%.")]
    public float MoneyDropRate { get; set; } = 1;

    /// <summary>
    /// Gets or sets the additional multiplier of the dropped zen when the killer plays with the MU Helper or is
    /// offline leveling.
    /// </summary>
    [Display(Name = "AFK money drop rate", Description = "Multiplicador extra del zen cuando el que mata usa el MU Helper o está en offlevel, entre 0 y 1. Con 0.5 recibe la mitad.")]
    public float AfkMoneyDropRate { get; set; } = 1;

    /// <summary>
    /// Gets or sets the chance multiplier of the valuable drops (jewels, excellent and ancient items) when the killer
    /// plays with the MU Helper or is offline leveling, between 0 and 1.
    /// </summary>
    [Display(Name = "AFK valuable drop rate", Description = "Multiplicador de la chance de joyas, excelentes y ancients cuando el que mata usa el MU Helper o está en offlevel, entre 0 y 1. Con 0.5 caen la mitad.")]
    public float AfkValuableDropRate { get; set; } = 1;
}
