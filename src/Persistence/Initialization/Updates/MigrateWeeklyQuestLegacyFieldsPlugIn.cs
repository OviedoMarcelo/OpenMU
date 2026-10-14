// <copyright file="MigrateWeeklyQuestLegacyFieldsPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.PlugIns.WeeklyQuests;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Moves the fields of the weekly quests, which the admin panel doesn't show anymore, to where they're edited now:
/// the single objective of a quest (the fields <see cref="WeeklyQuestDefinition.ObjectiveType"/>,
/// <see cref="WeeklyQuestDefinition.RequiredCount"/> etc.) into its steps (<see cref="WeeklyQuestDefinition.Objectives"/>),
/// and its points of the season pass (<see cref="WeeklyQuestDefinition.SeasonXp"/>) into a reward of the type
/// <see cref="WeeklyQuestRewardType.SeasonPoints"/>. What the quests require and give doesn't change.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("02F9D340-42AE-42DA-A3A6-BF9C0333FFE7")]
public class MigrateWeeklyQuestLegacyFieldsPlugIn : UpdatePlugInBase
{
    /// <summary>
    /// The plug-in name.
    /// </summary>
    internal const string PlugInName = "Move the objective and the season pass points of weekly quests";

    /// <summary>
    /// The plug-in description.
    /// </summary>
    internal const string PlugInDescription = "Weekly quests without steps get their single objective as the first step, and their points of the season pass become a reward \"Season pass points\". What the quests require and give doesn't change.";

    /// <summary>
    /// The properties of the single objective of a quest, which have the same name in <see cref="WeeklyQuestObjective"/>.
    /// </summary>
    private static readonly string[] SingleObjectiveProperties =
    [
        nameof(WeeklyQuestDefinition.ObjectiveType),
        nameof(WeeklyQuestDefinition.RequiredCount),
        nameof(WeeklyQuestDefinition.Monster),
        nameof(WeeklyQuestDefinition.NpcText),
        nameof(WeeklyQuestDefinition.Map),
        nameof(WeeklyQuestDefinition.Item),
        nameof(WeeklyQuestDefinition.MinimumItemLevel),
        nameof(WeeklyQuestDefinition.MiniGameType),
        nameof(WeeklyQuestDefinition.BossEncounterName),
        nameof(WeeklyQuestDefinition.MinimumVictimLevel),
        nameof(WeeklyQuestDefinition.IgnoreSameIp),
        nameof(WeeklyQuestDefinition.IgnoreSameGuild),
        nameof(WeeklyQuestDefinition.IgnoreSameParty),
        nameof(WeeklyQuestDefinition.VictimCooldownMinutes),
    ];

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    /// <remarks>
    /// Mandatory, because a quest loses its single objective and its points when it's saved in the admin panel,
    /// which doesn't show these fields anymore. It doesn't change what the quests require and give.
    /// </remarks>
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Moves the fields of every quest of the configuration.
    /// </summary>
    /// <param name="json">The serialized <see cref="WeeklyQuestsConfiguration"/>.</param>
    /// <returns>The migrated configuration, or <c>null</c> if no quest had to be changed.</returns>
    /// <remarks>
    /// The bonus for all completed quests (<see cref="WeeklyQuestsConfiguration.AllCompletedBonus"/>) is left alone:
    /// its objective is to complete the weekly quests, and its points of the pass are the default for weekly quests.
    /// </remarks>
    public static string? MigrateConfiguration(string json)
    {
        if (JsonNode.Parse(json) is not JsonObject configuration)
        {
            return null;
        }

        var changed = false;
        foreach (var quest in ValuesOf(configuration[nameof(WeeklyQuestsConfiguration.Quests)]).OfType<JsonObject>())
        {
            changed |= MoveSingleObjective(quest);
            changed |= MoveSeasonPoints(quest);
        }

        return changed ? configuration.ToJsonString() : null;
    }

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        foreach (var plugInConfiguration in gameConfiguration.PlugInConfigurations
                     .Where(c => c.TypeId == typeof(WeeklyQuestsPlugIn).GUID && !string.IsNullOrWhiteSpace(c.CustomConfiguration)))
        {
            if (MigrateConfiguration(plugInConfiguration.CustomConfiguration!) is { } migrated)
            {
                plugInConfiguration.CustomConfiguration = migrated;
            }
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Gets the elements of a serialized list, which is either an array or, with preserved references,
    /// an object with the elements in "$values".
    /// </summary>
    private static JsonArray? ArrayOf(JsonNode? list) => list switch
    {
        JsonArray array => array,
        JsonObject wrapper => wrapper["$values"] as JsonArray,
        _ => null,
    };

    private static IEnumerable<JsonNode?> ValuesOf(JsonNode? list) => ArrayOf(list) ?? [];

    /// <summary>
    /// Adds an element to a serialized list of a quest, and creates the list if the quest has none.
    /// </summary>
    private static void Add(JsonObject quest, string listProperty, JsonObject element)
    {
        if (ArrayOf(quest[listProperty]) is { } list)
        {
            list.Add(element);
        }
        else
        {
            quest[listProperty] = new JsonArray(element);
        }
    }

    private static bool MoveSingleObjective(JsonObject quest)
    {
        if (ArrayOf(quest[nameof(WeeklyQuestDefinition.Objectives)]) is { Count: > 0 })
        {
            return false;
        }

        var objective = new JsonObject();
        foreach (var property in SingleObjectiveProperties)
        {
            if (quest.Remove(property, out var value))
            {
                objective[property] = value;
            }
        }

        Add(quest, nameof(WeeklyQuestDefinition.Objectives), objective);
        return true;
    }

    private static bool MoveSeasonPoints(JsonObject quest)
    {
        if (!quest.Remove(nameof(WeeklyQuestDefinition.SeasonXp), out var value) || value is null)
        {
            return false;
        }

        var points = value.GetValue<int>();

        // With a reward of the points, the quest already gave these instead of its own points.
        if (points <= 0 || ValuesOf(quest[nameof(WeeklyQuestDefinition.Rewards)])
            .OfType<JsonObject>()
            .Any(r => r[nameof(WeeklyQuestReward.RewardType)]?.GetValue<int>() == (int)WeeklyQuestRewardType.SeasonPoints))
        {
            return true;
        }

        Add(quest, nameof(WeeklyQuestDefinition.Rewards), new JsonObject
        {
            [nameof(WeeklyQuestReward.RewardType)] = (int)WeeklyQuestRewardType.SeasonPoints,
            [nameof(WeeklyQuestReward.Amount)] = points,
        });
        return true;
    }
}
