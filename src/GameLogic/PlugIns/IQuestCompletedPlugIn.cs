// <copyright file="IQuestCompletedPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlugIns.WeeklyQuests;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// A plugin interface which is called when a player completed a quest of the <see cref="WeeklyQuestsPlugIn"/>
/// (a story chapter, a daily, weekly, class or zone quest).
/// </summary>
[Guid("ACCBEF9F-EE11-4F94-B544-CAD7589718B6")]
[PlugInPoint("Quest completed", "Plugins which will be executed when a player completed a quest of the quests plugin.")]
public interface IQuestCompletedPlugIn
{
    /// <summary>
    /// Is called when a player completed a quest. It's called once per quest and period,
    /// also when the rewards of the quest can't be handed out yet.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="quest">The quest.</param>
    ValueTask QuestCompletedAsync(Player player, WeeklyQuestDefinition quest);
}
