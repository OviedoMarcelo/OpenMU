// <copyright file="AchievementsPlugInTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlugIns.Achievements;
using MUnique.OpenMU.GameLogic.PlugIns.WeeklyQuests;
using MUnique.OpenMU.Persistence.Progression;

/// <summary>
/// Tests for the <see cref="AchievementsPlugIn"/>.
/// </summary>
[TestFixture]
public class AchievementsPlugInTest
{
    private const string CraftingAchievementId = "chaos-2";
    private const string CraftingTitleId = "chaos-master";

    /// <summary>
    /// Tests that the progress is counted, and that the reward and the title are given exactly once when the objective is reached.
    /// </summary>
    [Test]
    public async Task CompletesAchievementAndRewardsOnceAsync()
    {
        var player = await CreatePlayerAsync().ConfigureAwait(false);
        var plugIn = CreatePlugIn(new InMemoryProgressionRepository());

        await plugIn.ItemCraftedAsync(player, true, null).ConfigureAwait(false);
        Assert.That(player.Money, Is.EqualTo(0));

        await plugIn.ItemCraftedAsync(player, true, null).ConfigureAwait(false);
        Assert.That(player.Money, Is.EqualTo(1000));

        await plugIn.ItemCraftedAsync(player, true, null).ConfigureAwait(false);
        Assert.That(player.Money, Is.EqualTo(1000));

        var entry = (await plugIn.GetOverviewAsync(player).ConfigureAwait(false))!.Single();
        Assert.That(entry.Count, Is.EqualTo(2));
        Assert.That(entry.IsCompleted, Is.True);
        Assert.That(entry.IsRewarded, Is.True);

        var titles = await plugIn.GetTitlesAsync(player).ConfigureAwait(false);
        Assert.That(titles!.Value.Unlocked.Select(t => t.Id), Is.EquivalentTo(new[] { CraftingTitleId }));
    }

    /// <summary>
    /// Tests that failed craftings don't count.
    /// </summary>
    [Test]
    public async Task FailedCraftingDoesNotCountAsync()
    {
        var player = await CreatePlayerAsync().ConfigureAwait(false);
        var plugIn = CreatePlugIn(new InMemoryProgressionRepository());

        await plugIn.ItemCraftedAsync(player, false, null).ConfigureAwait(false);

        var entry = (await plugIn.GetOverviewAsync(player).ConfigureAwait(false))!.Single();
        Assert.That(entry.Count, Is.EqualTo(0));
    }

    /// <summary>
    /// Tests that a completed achievement is persisted, so that it's not rewarded again after a restart.
    /// </summary>
    [Test]
    public async Task CompletedAchievementIsPersistedAsync()
    {
        var repository = new InMemoryProgressionRepository();
        var player = await CreatePlayerAsync().ConfigureAwait(false);
        var plugIn = CreatePlugIn(repository);
        await plugIn.ItemCraftedAsync(player, true, null).ConfigureAwait(false);
        await plugIn.ItemCraftedAsync(player, true, null).ConfigureAwait(false);
        Assert.That(player.Money, Is.EqualTo(1000));

        // Leaving the game drops the in-memory state, so the next plugin has to load it from the repository.
        await plugIn.PlayerStateChangedAsync(player, PlayerState.EnteredWorld, PlayerState.CharacterSelection).ConfigureAwait(false);
        var restartedPlugIn = CreatePlugIn(repository);
        await restartedPlugIn.ItemCraftedAsync(player, true, null).ConfigureAwait(false);

        Assert.That(player.Money, Is.EqualTo(1000));
        var entry = (await restartedPlugIn.GetOverviewAsync(player).ConfigureAwait(false))!.Single();
        Assert.That(entry.Count, Is.EqualTo(2));
        Assert.That(entry.IsRewarded, Is.True);
        var titles = await restartedPlugIn.GetTitlesAsync(player).ConfigureAwait(false);
        Assert.That(titles!.Value.Unlocked, Has.Count.EqualTo(1));
    }

    /// <summary>
    /// Tests that the reward stays pending when it can't be given, and that it's given later.
    /// </summary>
    [Test]
    public async Task RewardStaysPendingUntilItFitsAsync()
    {
        var player = await CreatePlayerAsync().ConfigureAwait(false);
        player.GameContext.Configuration.MaximumInventoryMoney = 500;
        var plugIn = CreatePlugIn(new InMemoryProgressionRepository());

        await plugIn.ItemCraftedAsync(player, true, null).ConfigureAwait(false);
        await plugIn.ItemCraftedAsync(player, true, null).ConfigureAwait(false);
        Assert.That(player.Money, Is.EqualTo(0));

        var entry = (await plugIn.GetOverviewAsync(player).ConfigureAwait(false))!.Single();
        Assert.That(entry.IsCompleted, Is.True);
        Assert.That(entry.IsRewarded, Is.False);

        player.GameContext.Configuration.MaximumInventoryMoney = int.MaxValue;
        entry = (await plugIn.GetOverviewAsync(player).ConfigureAwait(false))!.Single();
        Assert.That(entry.IsRewarded, Is.True);
        Assert.That(player.Money, Is.EqualTo(1000));
    }

    /// <summary>
    /// Tests that a hidden achievement is only listed after it has been completed.
    /// </summary>
    [Test]
    public async Task HiddenAchievementIsListedWhenCompletedAsync()
    {
        var player = await CreatePlayerAsync().ConfigureAwait(false);
        var plugIn = CreatePlugIn(new InMemoryProgressionRepository());
        plugIn.Configuration!.Achievements.Single().IsHidden = true;

        await plugIn.ItemCraftedAsync(player, true, null).ConfigureAwait(false);
        Assert.That(await plugIn.GetOverviewAsync(player).ConfigureAwait(false), Is.Empty);

        await plugIn.ItemCraftedAsync(player, true, null).ConfigureAwait(false);
        Assert.That(await plugIn.GetOverviewAsync(player).ConfigureAwait(false), Has.Count.EqualTo(1));
    }

    /// <summary>
    /// Tests that the achievements which depend on a current value, like the number of resets,
    /// take the value instead of counting the events.
    /// </summary>
    [Test]
    public async Task AbsoluteAchievementTakesTheCurrentValueAsync()
    {
        var player = await CreatePlayerAsync().ConfigureAwait(false);
        var plugIn = CreatePlugIn(new InMemoryProgressionRepository());
        plugIn.Configuration!.Achievements.Add(new AchievementDefinition
        {
            Id = "resets-10",
            Name = "Resets",
            ObjectiveType = AchievementObjectiveType.ReachResets,
            RequiredCount = 10,
        });

        await plugIn.CharacterResetAsync(player, 7).ConfigureAwait(false);
        var entry = (await plugIn.GetOverviewAsync(player).ConfigureAwait(false))!.Single(e => e.Achievement.Id == "resets-10");
        Assert.That(entry.Count, Is.EqualTo(7));
        Assert.That(entry.IsCompleted, Is.False);

        await plugIn.CharacterResetAsync(player, 10).ConfigureAwait(false);
        entry = (await plugIn.GetOverviewAsync(player).ConfigureAwait(false))!.Single(e => e.Achievement.Id == "resets-10");
        Assert.That(entry.Count, Is.EqualTo(10));
        Assert.That(entry.IsCompleted, Is.True);
    }

    /// <summary>
    /// Tests that only unlocked titles can be shown, and that the shown title can be removed again.
    /// </summary>
    [Test]
    public async Task OnlyUnlockedTitlesCanBeShownAsync()
    {
        var player = await CreatePlayerAsync().ConfigureAwait(false);
        var plugIn = CreatePlugIn(new InMemoryProgressionRepository());

        Assert.That(await plugIn.SetActiveTitleAsync(player, CraftingTitleId).ConfigureAwait(false), Is.EqualTo(TitleChangeResult.NotUnlocked));
        Assert.That(await plugIn.SetActiveTitleAsync(player, "unknown").ConfigureAwait(false), Is.EqualTo(TitleChangeResult.Unknown));

        await plugIn.ItemCraftedAsync(player, true, null).ConfigureAwait(false);
        await plugIn.ItemCraftedAsync(player, true, null).ConfigureAwait(false);

        Assert.That(await plugIn.SetActiveTitleAsync(player, CraftingTitleId).ConfigureAwait(false), Is.EqualTo(TitleChangeResult.Changed));
        Assert.That(PlayerTitles.Get(player)?.Id, Is.EqualTo(CraftingTitleId));

        Assert.That(await plugIn.SetActiveTitleAsync(player, null).ConfigureAwait(false), Is.EqualTo(TitleChangeResult.Removed));
        Assert.That(PlayerTitles.Get(player), Is.Null);
    }

    /// <summary>
    /// Tests that the title of an achievement which was completed before its title was configured is unlocked later.
    /// </summary>
    [Test]
    public async Task TitleConfiguredAfterCompletionIsUnlockedAsync()
    {
        var player = await CreatePlayerAsync().ConfigureAwait(false);
        var plugIn = CreatePlugIn(new InMemoryProgressionRepository());
        var titles = plugIn.Configuration!.Titles;
        plugIn.Configuration.Titles = new List<TitleDefinition>();

        await plugIn.ItemCraftedAsync(player, true, null).ConfigureAwait(false);
        await plugIn.ItemCraftedAsync(player, true, null).ConfigureAwait(false);
        Assert.That((await plugIn.GetTitlesAsync(player).ConfigureAwait(false))!.Value.Unlocked, Is.Empty);

        plugIn.Configuration.Titles = titles;
        var unlocked = (await plugIn.GetTitlesAsync(player).ConfigureAwait(false))!.Value.Unlocked;
        Assert.That(unlocked.Select(t => t.Id), Is.EquivalentTo(new[] { CraftingTitleId }));
    }

    /// <summary>
    /// Tests that a title can be given to the whole account, and that removing it also hides it when it's shown.
    /// </summary>
    [Test]
    public async Task TitleCanBeGivenToTheAccountAndRemovedAsync()
    {
        var repository = new InMemoryProgressionRepository();
        var accountId = Guid.NewGuid();
        var player = await CreatePlayerAsync().ConfigureAwait(false);
        player.Account = new Persistence.BasicModel.Account { Id = accountId, LoginName = "test" };
        var plugIn = CreatePlugIn(repository);

        Assert.That(await plugIn.GrantTitleAsync(player, CraftingTitleId, "admin", forAccount: true).ConfigureAwait(false), Is.Not.Null);
        Assert.That((await repository.LoadUnlockedTitlesAsync([accountId]).ConfigureAwait(false)).Single().TitleId, Is.EqualTo(CraftingTitleId));
        Assert.That(await plugIn.SetActiveTitleAsync(player, CraftingTitleId).ConfigureAwait(false), Is.EqualTo(TitleChangeResult.Changed));

        Assert.That(await plugIn.RevokeTitleAsync(player, CraftingTitleId).ConfigureAwait(false), Is.True);
        Assert.That(PlayerTitles.Get(player), Is.Null);
        Assert.That((await plugIn.GetTitlesAsync(player).ConfigureAwait(false))!.Value.Unlocked, Is.Empty);
        Assert.That(await repository.LoadUnlockedTitlesAsync([accountId]).ConfigureAwait(false), Is.Empty);
        Assert.That(await plugIn.SetActiveTitleAsync(player, CraftingTitleId).ConfigureAwait(false), Is.EqualTo(TitleChangeResult.NotUnlocked));
    }

    /// <summary>
    /// Tests that a game master can unlock a title, e.g. as a prize of an event.
    /// </summary>
    [Test]
    public async Task TitleCanBeGrantedAsync()
    {
        var player = await CreatePlayerAsync().ConfigureAwait(false);
        var plugIn = CreatePlugIn(new InMemoryProgressionRepository());

        Assert.That(await plugIn.GrantTitleAsync(player, "unknown", "gm").ConfigureAwait(false), Is.Null);
        Assert.That((await plugIn.GrantTitleAsync(player, CraftingTitleId, "gm").ConfigureAwait(false))?.Id, Is.EqualTo(CraftingTitleId));
        Assert.That(await plugIn.SetActiveTitleAsync(player, CraftingTitleId).ConfigureAwait(false), Is.EqualTo(TitleChangeResult.Changed));
    }

    /// <summary>
    /// Tests that the progress of an achievement of the account is shared by its characters, but not by other accounts.
    /// </summary>
    [Test]
    public async Task AccountAchievementIsSharedByTheCharactersOfTheAccountAsync()
    {
        var repository = new InMemoryProgressionRepository();
        var accountId = Guid.NewGuid();
        var player = await CreatePlayerAsync().ConfigureAwait(false);
        player.Account = new Persistence.BasicModel.Account { Id = accountId, LoginName = "test" };
        var plugIn = CreatePlugIn(repository);
        plugIn.Configuration!.Achievements.Single().Scope = AchievementScope.Account;

        await plugIn.ItemCraftedAsync(player, true, null).ConfigureAwait(false);
        await plugIn.ItemCraftedAsync(player, true, null).ConfigureAwait(false);
        await plugIn.PlayerStateChangedAsync(player, PlayerState.EnteredWorld, PlayerState.CharacterSelection).ConfigureAwait(false);

        var stored = await repository.LoadAchievementsAsync([accountId]).ConfigureAwait(false);
        Assert.That(stored.Single().CompletedAt, Is.Not.Null);

        var sameAccount = await CreatePlayerAsync(player.GameContext).ConfigureAwait(false);
        sameAccount.Account = new Persistence.BasicModel.Account { Id = accountId, LoginName = "test" };
        var entry = (await plugIn.GetOverviewAsync(sameAccount).ConfigureAwait(false))!.Single();
        Assert.That(entry.IsCompleted, Is.True);
        Assert.That((await plugIn.GetTitlesAsync(sameAccount).ConfigureAwait(false))!.Value.Unlocked, Has.Count.EqualTo(1));

        var otherAccount = await CreatePlayerAsync(player.GameContext).ConfigureAwait(false);
        otherAccount.Account = new Persistence.BasicModel.Account { Id = Guid.NewGuid(), LoginName = "other" };
        entry = (await plugIn.GetOverviewAsync(otherAccount).ConfigureAwait(false))!.Single();
        Assert.That(entry.IsCompleted, Is.False);
    }

    private static AchievementsPlugIn CreatePlugIn(IProgressionRepository repository)
    {
        return new AchievementsPlugIn(repository)
        {
            Configuration = new AchievementsConfiguration
            {
                Titles = new List<TitleDefinition>
                {
                    new() { Id = CraftingTitleId, Text = "Chaos Master" },
                },
                Achievements = new List<AchievementDefinition>
                {
                    new()
                    {
                        Id = CraftingAchievementId,
                        Name = "Craftings",
                        ObjectiveType = AchievementObjectiveType.SuccessfulCraftings,
                        RequiredCount = 2,
                        TitleId = CraftingTitleId,
                        Rewards = new List<WeeklyQuestReward>
                        {
                            new() { RewardType = WeeklyQuestRewardType.Money, Amount = 1000 },
                        },
                    },
                },
            },
        };
    }

    private static async ValueTask<Player> CreatePlayerAsync(IGameContext? gameContext = null)
    {
        var player = gameContext is null
            ? await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false)
            : await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        player.GameContext.Configuration.MaximumInventoryMoney = int.MaxValue;
        await player.PlayerState.TryAdvanceToAsync(PlayerState.EnteredWorld).ConfigureAwait(false);
        return player;
    }
}
