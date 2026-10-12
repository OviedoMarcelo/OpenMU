// <copyright file="BossEncounterRanking.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.BossEncounters;

/// <summary>
/// A ranking of the players of a boss encounter.
/// </summary>
/// <remarks>
/// The values are stored in the configuration of the plugin, so they must not be renumbered.
/// </remarks>
public enum BossEncounterRanking
{
    /// <summary>
    /// By the damage to the boss.
    /// </summary>
    [Display(Name = "Daño")]
    Damage = 0,

    /// <summary>
    /// By the health restored to the players in the arena.
    /// </summary>
    [Display(Name = "Curación")]
    Healing = 1,
}
