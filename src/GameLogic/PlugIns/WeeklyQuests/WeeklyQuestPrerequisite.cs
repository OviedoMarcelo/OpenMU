// <copyright file="WeeklyQuestPrerequisite.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.WeeklyQuests;

using MUnique.OpenMU.DataModel.Composition;

/// <summary>
/// An option of the prerequisites of a quest, see <see cref="WeeklyQuestDefinition.PrerequisiteOptions"/>.
/// It's fulfilled when all of its quests are completed.
/// </summary>
public class WeeklyQuestPrerequisite
{
    /// <summary>
    /// Gets or sets the identifiers of the quests which all have to be completed.
    /// </summary>
    [Display(Name = "Hay que completar", Description = "Las quests de este camino. Hay que completarlas todas.")]
    [KeyOf(nameof(WeeklyQuestsConfiguration.Quests))]
    public ICollection<string> QuestIds { get; set; } = new List<string>();

    /// <summary>
    /// Determines whether all quests of this option are completed.
    /// </summary>
    /// <param name="isCompleted">Determines whether a quest is completed, by its id.</param>
    /// <returns><c>true</c>, if all quests are completed. An option without quests is never fulfilled.</returns>
    public bool IsFulfilled(Func<string, bool> isCompleted)
    {
        var hasQuests = false;
        foreach (var id in this.QuestIds)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            if (!isCompleted(id))
            {
                return false;
            }

            hasQuests = true;
        }

        return hasQuests;
    }

    /// <inheritdoc />
    public override string ToString() => string.Join(" + ", this.QuestIds);
}
