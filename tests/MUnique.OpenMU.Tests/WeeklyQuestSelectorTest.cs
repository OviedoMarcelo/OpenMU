// <copyright file="WeeklyQuestSelectorTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.PlugIns.WeeklyQuests;
using MUnique.OpenMU.Persistence.WeeklyQuests;

/// <summary>
/// Tests for the <see cref="WeeklyQuestSelector"/>.
/// </summary>
[TestFixture]
public class WeeklyQuestSelectorTest
{
    private static readonly DateTime Week1 = new(2026, 9, 21, 3, 0, 0, DateTimeKind.Utc);

    private static readonly WeeklyQuestCharacterInfo Character = new(new CharacterClass { Number = 0 }, 100, 5);

    /// <summary>
    /// Tests that without rotation, all active quests are part of the period.
    /// </summary>
    [Test]
    public void WithoutRotationAllActiveQuestsAreSelected()
    {
        var configuration = CreateConfiguration(10);
        configuration.Quests.First().IsActive = false;

        var quests = WeeklyQuestSelector.GetQuestsOfPeriod(configuration, Periods(Week1));

        Assert.That(quests, Has.Count.EqualTo(9));
    }

    /// <summary>
    /// Tests that the rotation draws the configured number of quests, plus the ones which are always included,
    /// and that the drawing is the same for the same week.
    /// </summary>
    [Test]
    public void RotationDrawsQuestsDeterministically()
    {
        var configuration = CreateConfiguration(10);
        configuration.QuestsPerWeek = 3;
        configuration.Quests.First().AlwaysIncluded = true;

        var quests = WeeklyQuestSelector.GetQuestsOfPeriod(configuration, Periods(Week1));
        var again = WeeklyQuestSelector.GetQuestsOfPeriod(configuration, Periods(Week1));

        Assert.That(quests, Has.Count.EqualTo(4));
        Assert.That(quests, Does.Contain(configuration.Quests.First()));
        Assert.That(again, Is.EqualTo(quests));
    }

    /// <summary>
    /// Tests that the rotation draws other quests in other weeks.
    /// </summary>
    [Test]
    public void RotationChangesBetweenWeeks()
    {
        var configuration = CreateConfiguration(20);
        configuration.QuestsPerWeek = 3;

        var selections = Enumerable.Range(0, 8)
            .Select(week => string.Join(",", WeeklyQuestSelector.GetQuestsOfPeriod(configuration, Periods(Week1.AddDays(7 * week))).Select(q => q.Id)))
            .ToHashSet();

        Assert.That(selections, Has.Count.GreaterThan(1));
    }

    /// <summary>
    /// Tests that the maximum level and resets hide a quest, unless the character already made progress in it.
    /// </summary>
    [Test]
    public void MaximumsHideQuestWithoutProgress()
    {
        var quest = new WeeklyQuestDefinition { Id = "q", MaximumLevel = 50 };
        Assert.That(WeeklyQuestSelector.IsAvailable(quest, Character, false, false), Is.False);
        Assert.That(WeeklyQuestSelector.IsAvailable(quest, Character, true, false), Is.True);

        quest = new WeeklyQuestDefinition { Id = "q", MaximumResets = 4 };
        Assert.That(WeeklyQuestSelector.IsAvailable(quest, Character, false, false), Is.False);

        quest = new WeeklyQuestDefinition { Id = "q", MaximumLevel = 100, MaximumResets = 5 };
        Assert.That(WeeklyQuestSelector.IsAvailable(quest, Character, false, false), Is.True);
    }

    /// <summary>
    /// Tests that a quest which was rewarded to another character of the account is never available.
    /// </summary>
    [Test]
    public void QuestBlockedByAccountIsNotAvailable()
    {
        var quest = new WeeklyQuestDefinition { Id = "q", OncePerAccount = true };
        Assert.That(WeeklyQuestSelector.IsAvailable(quest, Character, true, true), Is.False);
    }

    /// <summary>
    /// Tests that the bonus quest requires the number of quests and uses the configured rewards.
    /// </summary>
    [Test]
    public void AllCompletedBonusQuestIsCreatedOnlyWithRewards()
    {
        var configuration = CreateConfiguration(1);
        Assert.That(WeeklyQuestSelector.CreateAllCompletedBonusQuest(configuration, 3), Is.Null);

        configuration.AllCompletedBonus = new WeeklyQuestDefinition { Name = "Bonus" };
        Assert.That(WeeklyQuestSelector.CreateAllCompletedBonusQuest(configuration, 3), Is.Null);

        configuration.AllCompletedBonus.Rewards.Add(new WeeklyQuestReward { RewardType = WeeklyQuestRewardType.Money, Amount = 1 });
        var bonus = WeeklyQuestSelector.CreateAllCompletedBonusQuest(configuration, 3);
        Assert.That(bonus!.Id, Is.EqualTo(WeeklyQuestSelector.AllCompletedBonusId));
        Assert.That(bonus.RequiredCount, Is.EqualTo(3));
        Assert.That(bonus.Rewards, Has.Count.EqualTo(1));
    }

    /// <summary>
    /// Tests that the daily quests rotate by day, independent of the weekly rotation, and that quests which are done once never rotate.
    /// </summary>
    [Test]
    public void DailyQuestsRotateByDay()
    {
        var configuration = CreateConfiguration(0);
        for (var i = 0; i < 10; i++)
        {
            configuration.Quests.Add(new WeeklyQuestDefinition { Id = $"d{i}", Period = QuestPeriod.Daily });
        }

        configuration.Quests.Add(new WeeklyQuestDefinition { Id = "story", Period = QuestPeriod.Once });
        configuration.QuestsPerDay = 2;
        configuration.QuestsPerWeek = 1;

        var selections = Enumerable.Range(0, 7)
            .Select(day => WeeklyQuestSelector.GetQuestsOfPeriod(configuration, new QuestPeriodStarts(Week1, Week1.AddDays(day))))
            .ToList();

        Assert.That(selections.All(s => s.Count(q => q.Period == QuestPeriod.Daily) == 2), Is.True);
        Assert.That(selections.All(s => s.Any(q => q.Id == "story")), Is.True);
        Assert.That(selections.Select(s => string.Join(",", s.Select(q => q.Id))).ToHashSet(), Has.Count.GreaterThan(1));
    }

    /// <summary>
    /// Tests that a quest is hidden until its prerequisite has been completed.
    /// </summary>
    [Test]
    public void PrerequisiteLocksQuestUntilCompleted()
    {
        var configuration = CreateConfiguration(0);
        configuration.Quests.Add(new WeeklyQuestDefinition { Id = "chapter-1", Period = QuestPeriod.Once });
        configuration.Quests.Add(new WeeklyQuestDefinition { Id = "chapter-2", Period = QuestPeriod.Once, PrerequisiteQuestId = "chapter-1" });
        var progress = new Dictionary<string, WeeklyQuestProgress>();

        var available = WeeklyQuestSelector.GetAvailableQuests(configuration, Periods(Week1), Character, progress, new HashSet<string>());
        Assert.That(available.Select(q => q.Id), Is.EqualTo(new[] { "chapter-1" }));

        progress["chapter-1"] = new WeeklyQuestProgress { QuestId = "chapter-1", Count = 1 };
        available = WeeklyQuestSelector.GetAvailableQuests(configuration, Periods(Week1), Character, progress, new HashSet<string>());
        Assert.That(available.Select(q => q.Id), Is.EqualTo(new[] { "chapter-1" }));

        progress["chapter-1"].CompletedAt = DateTime.UtcNow;
        available = WeeklyQuestSelector.GetAvailableQuests(configuration, Periods(Week1), Character, progress, new HashSet<string>());
        Assert.That(available.Select(q => q.Id), Is.EqualTo(new[] { "chapter-1", "chapter-2" }));
    }

    /// <summary>
    /// Tests that the quests of a chapter are hidden until the required number of quests of the previous chapter are completed.
    /// </summary>
    [Test]
    public void ChapterUnlocksAfterRequiredQuestsOfPreviousChapter()
    {
        var configuration = CreateChapterConfiguration(questsToComplete: 2);
        var progress = new Dictionary<string, WeeklyQuestProgress>();

        Assert.That(AvailableIds(configuration, progress), Is.EqualTo(new[] { "1a", "1b", "1c" }));

        Complete(progress, "1a");
        Assert.That(AvailableIds(configuration, progress), Is.EqualTo(new[] { "1a", "1b", "1c" }));

        Complete(progress, "1c");
        Assert.That(AvailableIds(configuration, progress), Is.EqualTo(new[] { "1a", "1b", "1c", "2a", "2-daily" }));
    }

    /// <summary>
    /// Tests that a chapter without a number of quests to complete requires all of its quests which are done once,
    /// and that its daily quests don't count.
    /// </summary>
    [Test]
    public void ChapterRequiresAllOnceQuestsByDefault()
    {
        var configuration = CreateChapterConfiguration(questsToComplete: 0);
        configuration.Quests.Add(new WeeklyQuestDefinition { Id = "1-daily", Period = QuestPeriod.Daily, ChapterId = "c1" });
        var progress = new Dictionary<string, WeeklyQuestProgress>();
        Complete(progress, "1a");
        Complete(progress, "1b");
        Complete(progress, "1-daily");

        Assert.That(WeeklyQuestSelector.GetLockedChapters(configuration, Character, progress), Does.Contain("c2"));

        Complete(progress, "1c");
        Assert.That(WeeklyQuestSelector.GetLockedChapters(configuration, Character, progress), Is.Empty);
    }

    /// <summary>
    /// Tests that a chapter is locked as long as the chapter which it requires is locked, even if its own required chapter has progress.
    /// </summary>
    [Test]
    public void ChaptersUnlockOneAfterAnother()
    {
        var configuration = CreateChapterConfiguration(questsToComplete: 1);
        configuration.Chapters.Add(new WeeklyQuestChapter { Id = "c3", PrerequisiteChapterId = "c2" });
        var progress = new Dictionary<string, WeeklyQuestProgress>();

        // Progress of the second chapter, e.g. from before it got a prerequisite, doesn't unlock the third one.
        Complete(progress, "2a");
        Assert.That(WeeklyQuestSelector.GetLockedChapters(configuration, Character, progress), Is.EquivalentTo(new[] { "c2", "c3" }));

        Complete(progress, "1a");
        Assert.That(WeeklyQuestSelector.GetLockedChapters(configuration, Character, progress), Is.Empty);
    }

    /// <summary>
    /// Tests that unknown chapters don't lock quests, and that chapters which require each other don't hang.
    /// </summary>
    [Test]
    public void MisconfiguredChaptersDontHang()
    {
        var configuration = CreateConfiguration(0);
        configuration.Chapters.Add(new WeeklyQuestChapter { Id = "a", PrerequisiteChapterId = "b" });
        configuration.Chapters.Add(new WeeklyQuestChapter { Id = "b", PrerequisiteChapterId = "a" });
        configuration.Chapters.Add(new WeeklyQuestChapter { Id = "c", PrerequisiteChapterId = "unknown" });
        configuration.Quests.Add(new WeeklyQuestDefinition { Id = "in-a", Period = QuestPeriod.Once, ChapterId = "a" });
        configuration.Quests.Add(new WeeklyQuestDefinition { Id = "in-c", Period = QuestPeriod.Once, ChapterId = "c" });
        configuration.Quests.Add(new WeeklyQuestDefinition { Id = "in-unknown", Period = QuestPeriod.Once, ChapterId = "unknown" });

        Assert.That(AvailableIds(configuration, new Dictionary<string, WeeklyQuestProgress>()), Is.EqualTo(new[] { "in-c", "in-unknown" }));
    }

    /// <summary>
    /// Tests that the entry of a quest with several objectives counts the done objectives and knows the current step.
    /// </summary>
    [Test]
    public void EntryOfQuestWithObjectivesShowsSteps()
    {
        var quest = new WeeklyQuestDefinition
        {
            Id = "story",
            SequentialObjectives = true,
            Objectives =
            {
                new WeeklyQuestObjective { ObjectiveType = WeeklyQuestObjectiveType.TalkToNpc },
                new WeeklyQuestObjective { ObjectiveType = WeeklyQuestObjectiveType.KillAnyMonster, RequiredCount = 50 },
                new WeeklyQuestObjective { ObjectiveType = WeeklyQuestObjectiveType.GainResets },
            },
        };
        var progress = new WeeklyQuestProgress { QuestId = "story", Count = 1 };
        progress.SetCount(1, 20);

        var entry = WeeklyQuestSelector.CreateEntry(quest, new Dictionary<string, WeeklyQuestProgress> { ["story"] = progress });

        Assert.That(entry.Count, Is.EqualTo(1));
        Assert.That(entry.Required, Is.EqualTo(3));
        Assert.That(entry.CurrentStep, Is.EqualTo(1));
        Assert.That(entry.Objectives.Select(o => o.IsDone), Is.EqualTo(new[] { true, false, false }));
        Assert.That(entry.Objectives[1].Count, Is.EqualTo(20));
        Assert.That(entry.Objectives[1].Required, Is.EqualTo(50));
    }

    /// <summary>
    /// Tests that a quest without objectives uses its single objective, like before objectives existed.
    /// </summary>
    [Test]
    public void QuestWithoutObjectivesHasItsSingleObjective()
    {
        var quest = new WeeklyQuestDefinition { Id = "q", ObjectiveType = WeeklyQuestObjectiveType.GainResets, RequiredCount = 3 };

        var objective = quest.GetObjectives().Single();

        Assert.That(objective.ObjectiveType, Is.EqualTo(WeeklyQuestObjectiveType.GainResets));
        Assert.That(objective.RequiredCount, Is.EqualTo(3));
        Assert.That(objective.GetDisplayText(System.Globalization.CultureInfo.InvariantCulture), Is.EqualTo("Hacé 3 resets"));
    }

    private static QuestPeriodStarts Periods(DateTime weekStart) => new(weekStart, weekStart);

    private static string[] AvailableIds(WeeklyQuestsConfiguration configuration, Dictionary<string, WeeklyQuestProgress> progress)
    {
        return WeeklyQuestSelector.GetAvailableQuests(configuration, Periods(Week1), Character, progress, new HashSet<string>())
            .Select(q => q.Id)
            .ToArray();
    }

    private static void Complete(Dictionary<string, WeeklyQuestProgress> progress, string questId)
    {
        progress[questId] = new WeeklyQuestProgress { QuestId = questId, Count = 1, CompletedAt = DateTime.UtcNow };
    }

    /// <summary>
    /// Creates a configuration with a first chapter of three quests, and a second chapter with a quest and a daily quest.
    /// </summary>
    private static WeeklyQuestsConfiguration CreateChapterConfiguration(int questsToComplete)
    {
        var configuration = CreateConfiguration(0);
        configuration.Chapters.Add(new WeeklyQuestChapter { Id = "c1", Name = "Capítulo I", QuestsToComplete = questsToComplete });
        configuration.Chapters.Add(new WeeklyQuestChapter { Id = "c2", Name = "Capítulo II", PrerequisiteChapterId = "c1", QuestsToComplete = questsToComplete });
        foreach (var id in new[] { "1a", "1b", "1c" })
        {
            configuration.Quests.Add(new WeeklyQuestDefinition { Id = id, Period = QuestPeriod.Once, ChapterId = "c1" });
        }

        configuration.Quests.Add(new WeeklyQuestDefinition { Id = "2a", Period = QuestPeriod.Once, ChapterId = "c2" });
        configuration.Quests.Add(new WeeklyQuestDefinition { Id = "2-daily", Period = QuestPeriod.Daily, ChapterId = "c2" });
        return configuration;
    }

    private static WeeklyQuestsConfiguration CreateConfiguration(int questCount)
    {
        return new WeeklyQuestsConfiguration
        {
            Quests = Enumerable.Range(0, questCount)
                .Select(i => new WeeklyQuestDefinition { Id = $"q{i}", Name = $"Quest {i}" })
                .ToList(),
        };
    }
}
