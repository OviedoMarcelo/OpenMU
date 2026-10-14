// <copyright file="MigrateWeeklyQuestLegacyFieldsTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Tests;

using System.Text.Json;
using System.Text.Json.Serialization;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.PlugIns.WeeklyQuests;
using MUnique.OpenMU.Persistence.Initialization.Updates;

/// <summary>
/// Tests the <see cref="MigrateWeeklyQuestLegacyFieldsPlugIn"/>.
/// </summary>
[TestFixture]
internal class MigrateWeeklyQuestLegacyFieldsTest
{
    /// <summary>
    /// Tests that the single objective of a quest without steps becomes its first step, that its points of the
    /// season pass become a reward, that the rest stays as it is, and that migrating again changes nothing.
    /// </summary>
    /// <param name="preserveReferences">Whether the configuration was serialized with preserved references, so the lists are wrapped in "$values".</param>
    [TestCase(false)]
    [TestCase(true)]
    public void MovesLegacyFields(bool preserveReferences)
    {
        var options = new JsonSerializerOptions { ReferenceHandler = preserveReferences ? ReferenceHandler.Preserve : null };
        var configuration = new WeeklyQuestsConfiguration
        {
            Quests = new List<WeeklyQuestDefinition>
            {
                new()
                {
                    Id = "single",
                    ObjectiveType = WeeklyQuestObjectiveType.CompleteMiniGame,
                    MiniGameType = MiniGameType.BloodCastle,
                    RequiredCount = 3,
                    IgnoreSameIp = false,
                    SeasonXp = 150,
                    Rewards = new List<WeeklyQuestReward> { new() { RewardType = WeeklyQuestRewardType.Money, Amount = 1000 } },
                },
                new()
                {
                    Id = "steps",
                    ObjectiveType = WeeklyQuestObjectiveType.GainResets,
                    Objectives = new List<WeeklyQuestObjective>
                    {
                        new() { ObjectiveType = WeeklyQuestObjectiveType.KillAnyMonster, RequiredCount = 50 },
                    },
                },
            },
            AllCompletedBonus = new WeeklyQuestDefinition { Id = "bonus" },
        };

        var migrated = MigrateWeeklyQuestLegacyFieldsPlugIn.MigrateConfiguration(JsonSerializer.Serialize(configuration, options));

        Assert.That(migrated, Is.Not.Null);
        var result = JsonSerializer.Deserialize<WeeklyQuestsConfiguration>(migrated!, options)!;
        var singleQuest = result.Quests.Single(q => q.Id == "single");
        var single = singleQuest.Objectives.Single();
        Assert.That(single.ObjectiveType, Is.EqualTo(WeeklyQuestObjectiveType.CompleteMiniGame));
        Assert.That(single.MiniGameType, Is.EqualTo(MiniGameType.BloodCastle));
        Assert.That(single.RequiredCount, Is.EqualTo(3));
        Assert.That(single.IgnoreSameIp, Is.False);
        Assert.That(singleQuest.SeasonXp, Is.Zero);
        Assert.That(singleQuest.Rewards.Select(r => (r.RewardType, r.Amount)), Is.EquivalentTo(new[]
        {
            (WeeklyQuestRewardType.Money, 1000),
            (WeeklyQuestRewardType.SeasonPoints, 150),
        }));
        Assert.That(singleQuest.GetSeasonPoints(), Is.EqualTo(150));

        var steps = result.Quests.Single(q => q.Id == "steps").Objectives.Single();
        Assert.That(steps.ObjectiveType, Is.EqualTo(WeeklyQuestObjectiveType.KillAnyMonster));
        Assert.That(steps.RequiredCount, Is.EqualTo(50));

        Assert.That(result.AllCompletedBonus!.Objectives, Is.Empty, "The objective of the bonus is to complete the weekly quests.");

        Assert.That(MigrateWeeklyQuestLegacyFieldsPlugIn.MigrateConfiguration(migrated!), Is.Null);
    }
}
