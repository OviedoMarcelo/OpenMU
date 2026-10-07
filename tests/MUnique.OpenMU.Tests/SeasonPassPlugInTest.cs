// <copyright file="SeasonPassPlugInTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlugIns.SeasonPass;
using MUnique.OpenMU.GameLogic.PlugIns.WeeklyQuests;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.Progression;

/// <summary>
/// Tests for the <see cref="SeasonPassPlugIn"/>.
/// </summary>
[TestFixture]
public class SeasonPassPlugInTest
{
    private const int ExperiencePerLevel = 100;

    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Tests that a completed quest gives the experience of the pass for its period, or its own.
    /// </summary>
    [Test]
    public async Task CompletedQuestGivesExperienceAsync()
    {
        var player = await CreatePlayerAsync().ConfigureAwait(false);
        var plugIn = CreatePlugIn(new InMemoryProgressionRepository());

        await plugIn.QuestCompletedAsync(player, new WeeklyQuestDefinition { Period = QuestPeriod.Daily }).ConfigureAwait(false);
        Assert.That((await plugIn.GetOverviewAsync(player).ConfigureAwait(false))!.Experience, Is.EqualTo(50));

        await plugIn.QuestCompletedAsync(player, new WeeklyQuestDefinition { Period = QuestPeriod.Weekly, SeasonXp = 7 }).ConfigureAwait(false);
        var overview = await plugIn.GetOverviewAsync(player).ConfigureAwait(false);
        Assert.That(overview!.Experience, Is.EqualTo(57));
        Assert.That(overview.Level, Is.EqualTo(0));
    }

    /// <summary>
    /// Tests that the free rewards of the reached levels are handed out once, and the premium ones only with the premium pass.
    /// </summary>
    [Test]
    public async Task ClaimsFreeRewardsOnceAndPremiumOnlyWithPremiumAsync()
    {
        var player = await CreatePlayerAsync().ConfigureAwait(false);
        var plugIn = CreatePlugIn(new InMemoryProgressionRepository());
        await AddLevelsAsync(plugIn, player, 2).ConfigureAwait(false);

        Assert.That(await plugIn.ClaimAsync(player).ConfigureAwait(false), Is.EqualTo(2));
        Assert.That(player.Money, Is.EqualTo(1000 + 2000));
        Assert.That(await plugIn.ClaimAsync(player).ConfigureAwait(false), Is.EqualTo(0));
        Assert.That(player.Money, Is.EqualTo(3000));

        var granted = await plugIn.GrantPremiumAsync(player.GameContext, player.Account!.GetId(), "gm").ConfigureAwait(false);
        Assert.That(granted?.WasActive, Is.False);
        Assert.That(await plugIn.ClaimAsync(player).ConfigureAwait(false), Is.EqualTo(2));
        Assert.That(player.Money, Is.EqualTo(3000 + 10_000 + 20_000));

        var overview = await plugIn.GetOverviewAsync(player).ConfigureAwait(false);
        Assert.That(overview!.Levels.Take(2).All(l => l.IsFreeClaimed && l.IsPremiumClaimed), Is.True);
        Assert.That(overview.ClaimableCount, Is.EqualTo(0));
    }

    /// <summary>
    /// Tests that the experience and the claims are persisted, so that nothing is lost or handed out twice after a restart.
    /// </summary>
    [Test]
    public async Task ProgressIsPersistedAsync()
    {
        var repository = new InMemoryProgressionRepository();
        var player = await CreatePlayerAsync().ConfigureAwait(false);
        var plugIn = CreatePlugIn(repository);
        await plugIn.QuestCompletedAsync(player, new WeeklyQuestDefinition { SeasonXp = 130 }).ConfigureAwait(false);
        await plugIn.ClaimAsync(player).ConfigureAwait(false);
        Assert.That(player.Money, Is.EqualTo(1000));

        await plugIn.PlayerStateChangedAsync(player, PlayerState.EnteredWorld, PlayerState.CharacterSelection).ConfigureAwait(false);
        var restartedPlugIn = CreatePlugIn(repository);
        var overview = await restartedPlugIn.GetOverviewAsync(player).ConfigureAwait(false);

        Assert.That(overview!.Experience, Is.EqualTo(130));
        Assert.That(overview.Levels[0].IsFreeClaimed, Is.True);
        Assert.That(await restartedPlugIn.ClaimAsync(player).ConfigureAwait(false), Is.EqualTo(0));
        Assert.That(player.Money, Is.EqualTo(1000));
    }

    /// <summary>
    /// Tests that a reward which doesn't fit stays claimable, and that the levels after it wait for it.
    /// </summary>
    [Test]
    public async Task RewardStaysClaimableUntilItFitsAsync()
    {
        var player = await CreatePlayerAsync().ConfigureAwait(false);
        player.GameContext.Configuration.MaximumInventoryMoney = 500;
        var plugIn = CreatePlugIn(new InMemoryProgressionRepository());
        await AddLevelsAsync(plugIn, player, 1).ConfigureAwait(false);

        Assert.That(await plugIn.ClaimAsync(player).ConfigureAwait(false), Is.EqualTo(0));
        Assert.That((await plugIn.GetOverviewAsync(player).ConfigureAwait(false))!.ClaimableCount, Is.EqualTo(1));

        player.GameContext.Configuration.MaximumInventoryMoney = int.MaxValue;
        Assert.That(await plugIn.ClaimAsync(player).ConfigureAwait(false), Is.EqualTo(1));
        Assert.That(player.Money, Is.EqualTo(1000));
    }

    /// <summary>
    /// Tests that the level doesn't go beyond the highest configured level.
    /// </summary>
    [Test]
    public async Task LevelStopsAtTheMaximumAsync()
    {
        var player = await CreatePlayerAsync().ConfigureAwait(false);
        var plugIn = CreatePlugIn(new InMemoryProgressionRepository());
        await plugIn.QuestCompletedAsync(player, new WeeklyQuestDefinition { SeasonXp = 10 * ExperiencePerLevel }).ConfigureAwait(false);

        Assert.That((await plugIn.GetOverviewAsync(player).ConfigureAwait(false))!.Level, Is.EqualTo(3));
    }

    /// <summary>
    /// Tests that nothing is gained outside of a running season.
    /// </summary>
    [Test]
    public async Task NothingIsGainedWithoutRunningSeasonAsync()
    {
        var player = await CreatePlayerAsync().ConfigureAwait(false);
        var plugIn = CreatePlugIn(new InMemoryProgressionRepository());
        plugIn.UtcNow = () => new DateTime(2028, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        await plugIn.QuestCompletedAsync(player, new WeeklyQuestDefinition { SeasonXp = 500 }).ConfigureAwait(false);

        Assert.That(await plugIn.GetOverviewAsync(player).ConfigureAwait(false), Is.SameAs(SeasonPassOverview.None));
        Assert.That(await plugIn.GrantPremiumAsync(player.GameContext, player.Account!.GetId(), "gm").ConfigureAwait(false), Is.Null);
    }

    private static async ValueTask AddLevelsAsync(SeasonPassPlugIn plugIn, Player player, int levels)
    {
        await plugIn.QuestCompletedAsync(player, new WeeklyQuestDefinition { SeasonXp = levels * ExperiencePerLevel }).ConfigureAwait(false);
    }

    private static SeasonPassPlugIn CreatePlugIn(IProgressionRepository repository)
    {
        return new SeasonPassPlugIn(repository)
        {
            UtcNow = () => Now,
            Configuration = new SeasonPassConfiguration
            {
                DailyQuestExperience = 50,
                WeeklyQuestExperience = 80,
                Seasons = new List<SeasonDefinition>
                {
                    new()
                    {
                        Id = "test",
                        Name = "Test",
                        Start = new DateTime(2026, 1, 1),
                        End = new DateTime(2027, 1, 1),
                        ExperiencePerLevel = ExperiencePerLevel,
                        Levels = Enumerable.Range(1, 3).Select(level => new SeasonLevelDefinition
                        {
                            Level = level,
                            FreeRewards = new List<WeeklyQuestReward> { new() { RewardType = WeeklyQuestRewardType.Money, Amount = 1000 * level } },
                            PremiumRewards = new List<WeeklyQuestReward> { new() { RewardType = WeeklyQuestRewardType.Money, Amount = 10_000 * level } },
                        }).ToList(),
                    },
                },
            },
        };
    }

    private static async ValueTask<Player> CreatePlayerAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.GameContext.Configuration.MaximumInventoryMoney = int.MaxValue;
        player.Account = new Persistence.BasicModel.Account { Id = Guid.NewGuid(), LoginName = "test" };
        await player.PlayerState.TryAdvanceToAsync(PlayerState.EnteredWorld).ConfigureAwait(false);
        return player;
    }
}
