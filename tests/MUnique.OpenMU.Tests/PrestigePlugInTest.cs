// <copyright file="PrestigePlugInTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.GameLogic.PlugIns.Prestige;
using MUnique.OpenMU.GameLogic.PlugIns.WeeklyQuests;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.Progression;

/// <summary>
/// Tests for the <see cref="PrestigePlugIn"/>.
/// </summary>
[TestFixture]
public class PrestigePlugInTest
{
    private const int MaximumMasterLevel = 200;

    /// <summary>
    /// Tests that the prestige requires the configured level and the maximum master level.
    /// </summary>
    [Test]
    public async Task RequiresLevelAndMasterLevelAsync()
    {
        var player = await CreatePlayerAsync(level: 399, masterLevel: MaximumMasterLevel).ConfigureAwait(false);
        var plugIn = CreatePlugIn(new InMemoryProgressionRepository());
        Assert.That(plugIn.CheckRequirements(player, 0), Is.EqualTo(PrestigeResult.LevelTooLow));

        player.Attributes![Stats.Level] = 400;
        player.Attributes[Stats.MasterLevel] = MaximumMasterLevel - 1;
        Assert.That(plugIn.CheckRequirements(player, 0), Is.EqualTo(PrestigeResult.MasterLevelTooLow));

        player.Attributes[Stats.MasterLevel] = MaximumMasterLevel;
        Assert.That(plugIn.CheckRequirements(player, 0), Is.EqualTo(PrestigeResult.Done));
        Assert.That(plugIn.CheckRequirements(player, 2), Is.EqualTo(PrestigeResult.MaximumReached));
    }

    /// <summary>
    /// Tests that the prestige starts the master level over, keeps the level and hands out the points and rewards.
    /// </summary>
    [Test]
    public async Task PrestigeStartsMasterLevelOverAsync()
    {
        var repository = new InMemoryProgressionRepository();
        var player = await CreatePlayerAsync(level: 400, masterLevel: MaximumMasterLevel).ConfigureAwait(false);
        var character = player.SelectedCharacter!;
        character.MasterExperience = 123_456;
        character.MasterLevelUpPoints = 5;
        var masterSkill = new SkillEntry { Skill = new Skill { Number = 300, MasterDefinition = new MasterSkillDefinition() }, Level = 10 };
        var normalSkill = new SkillEntry { Skill = new Skill { Number = 1 } };
        character.LearnedSkills.Add(masterSkill);
        character.LearnedSkills.Add(normalSkill);
        var plugIn = CreatePlugIn(repository);

        Assert.That(await plugIn.PrestigeAsync(player).ConfigureAwait(false), Is.EqualTo(PrestigeResult.Done));

        Assert.That(player.Attributes![Stats.MasterLevel], Is.EqualTo(0));
        Assert.That(player.Level, Is.EqualTo(400));
        Assert.That(character.MasterExperience, Is.EqualTo(0));
        Assert.That(character.MasterLevelUpPoints, Is.EqualTo(0));
        Assert.That(character.LearnedSkills, Is.EquivalentTo(new[] { normalSkill }));
        Assert.That(character.Inventory!.Money, Is.EqualTo(1000));

        var stored = (await repository.LoadPrestigeAsync([character.GetId()]).ConfigureAwait(false)).Single();
        Assert.That(stored.Level, Is.EqualTo(1));
        Assert.That(stored.Points, Is.EqualTo(3));
    }

    /// <summary>
    /// Tests that nothing changes when the rewards of the prestige don't fit.
    /// </summary>
    [Test]
    public async Task NothingChangesWhenRewardsDontFitAsync()
    {
        var player = await CreatePlayerAsync(level: 400, masterLevel: MaximumMasterLevel).ConfigureAwait(false);
        player.GameContext.Configuration.MaximumInventoryMoney = 500;
        var repository = new InMemoryProgressionRepository();
        var plugIn = CreatePlugIn(repository);

        Assert.That(await plugIn.PrestigeAsync(player).ConfigureAwait(false), Is.EqualTo(PrestigeResult.RewardsDontFit));
        Assert.That(player.Attributes![Stats.MasterLevel], Is.EqualTo(MaximumMasterLevel));
        Assert.That(await repository.LoadPrestigeAsync([player.SelectedCharacter!.GetId()]).ConfigureAwait(false), Is.Empty);
    }

    /// <summary>
    /// Tests that the prestige gives an experience bonus per level, up to the maximum.
    /// </summary>
    [Test]
    public async Task ExperienceBonusGrowsUpToTheMaximumAsync()
    {
        var repository = new InMemoryProgressionRepository();
        var player = await CreatePlayerAsync(level: 400, masterLevel: MaximumMasterLevel).ConfigureAwait(false);
        await repository.SavePrestigeAsync(new PrestigeProgress { CharacterId = player.SelectedCharacter!.GetId(), Level = 20 }).ConfigureAwait(false);
        var plugIn = CreatePlugIn(repository);
        plugIn.Configuration!.MaximumPrestige = 0;
        await plugIn.PlayerStateChangedAsync(player, PlayerState.CharacterSelection, PlayerState.EnteredWorld).ConfigureAwait(false);

        var args = new ExperienceCalculationArgs(player, false, 1000);
        await plugIn.CalculateExperienceAsync(player, args).ConfigureAwait(false);

        // 20 prestige levels with 2% each would be 40%, but the maximum is 10%.
        Assert.That(args.Experience, Is.EqualTo(1100).Within(0.001));
    }

    private static PrestigePlugIn CreatePlugIn(IProgressionRepository repository)
    {
        return new PrestigePlugIn(repository)
        {
            Configuration = new PrestigeConfiguration
            {
                RequiredLevel = 400,
                MaximumPrestige = 2,
                PointsPerPrestige = 3,
                ExperienceBonusPercentPerPrestige = 2,
                MaximumExperienceBonusPercent = 10,
                AnnouncementMessage = string.Empty,
                Levels = new List<PrestigeLevelDefinition>
                {
                    new()
                    {
                        Level = 1,
                        Rewards = new List<WeeklyQuestReward> { new() { RewardType = WeeklyQuestRewardType.Money, Amount = 1000 } },
                    },
                },
            },
        };
    }

    private static async ValueTask<Player> CreatePlayerAsync(int level, int masterLevel)
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.GameContext.Configuration.MaximumInventoryMoney = int.MaxValue;
        player.GameContext.Configuration.MaximumMasterLevel = MaximumMasterLevel;
        player.Account = new Persistence.BasicModel.Account { Id = Guid.NewGuid(), LoginName = "test" };
        await player.PlayerState.TryAdvanceToAsync(PlayerState.EnteredWorld).ConfigureAwait(false);
        player.Attributes![Stats.Level] = level;
        player.Attributes[Stats.MasterLevel] = masterLevel;
        return player;
    }
}
