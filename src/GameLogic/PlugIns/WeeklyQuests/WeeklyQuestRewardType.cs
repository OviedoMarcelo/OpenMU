// <copyright file="WeeklyQuestRewardType.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.WeeklyQuests;

/// <summary>
/// The type of a <see cref="WeeklyQuestReward"/>.
/// </summary>
public enum WeeklyQuestRewardType
{
    /// <summary>
    /// An item which is added to the inventory.
    /// </summary>
    [Display(Name = "Item")]
    Item,

    /// <summary>
    /// Zen.
    /// </summary>
    [Display(Name = "Zen")]
    Money,

    /// <summary>
    /// Experience.
    /// </summary>
    [Display(Name = "Experiencia")]
    Experience,

    /// <summary>
    /// Master experience.
    /// </summary>
    [Display(Name = "Experiencia master")]
    MasterExperience,

    /// <summary>
    /// Points of the season pass. They're given by the season pass when a quest is completed
    /// (see <see cref="IQuestCompletedPlugIn"/>), instead of the default points for the period of the quest.
    /// </summary>
    [Display(Name = "Puntos del pase")]
    SeasonPoints,
}
