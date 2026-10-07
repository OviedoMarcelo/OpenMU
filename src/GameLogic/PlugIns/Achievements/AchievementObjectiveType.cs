// <copyright file="AchievementObjectiveType.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.Achievements;

/// <summary>
/// The type of the objective of an <see cref="AchievementDefinition"/>.
/// </summary>
/// <remarks>
/// The values are stored in the plugin configuration, so new members have to be appended at the end.
/// </remarks>
public enum AchievementObjectiveType
{
    /// <summary>
    /// Kill monsters: any monster, or a specific one.
    /// </summary>
    [Display(Name = "Matar monstruos")]
    KillMonsters = 0,

    /// <summary>
    /// Stay in a mini game (e.g. Blood Castle) until its end.
    /// </summary>
    [Display(Name = "Completar eventos")]
    CompleteMiniGames = 1,

    /// <summary>
    /// Get an item of at least a level, by upgrading it with a jewel, the chaos machine or by picking it up.
    /// </summary>
    [Display(Name = "Conseguir un item +N")]
    ObtainItemLevel = 2,

    /// <summary>
    /// Win duels.
    /// </summary>
    [Display(Name = "Ganar duelos")]
    WinDuels = 3,

    /// <summary>
    /// Successful mixes of the chaos machine (or another crafting NPC).
    /// </summary>
    [Display(Name = "Combinaciones exitosas")]
    SuccessfulCraftings = 4,

    /// <summary>
    /// Reach a character level. The required count is the level.
    /// </summary>
    [Display(Name = "Llegar a nivel")]
    ReachLevel = 5,

    /// <summary>
    /// Reach a master level. The required count is the master level.
    /// </summary>
    [Display(Name = "Llegar a nivel master")]
    ReachMasterLevel = 6,

    /// <summary>
    /// Kill other players.
    /// </summary>
    [Display(Name = "Matar jugadores")]
    KillPlayers = 7,

    /// <summary>
    /// Reach a number of resets. The required count is the number of resets.
    /// </summary>
    [Display(Name = "Llegar a resets")]
    ReachResets = 8,
}
