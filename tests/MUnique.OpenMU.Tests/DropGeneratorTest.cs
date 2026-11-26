// <copyright file="DropGeneratorTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Moq;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.PlugIns.Economy;

/// <summary>
/// Tests the drop generator.
/// </summary>
[TestFixture]
public class DropGeneratorTest
{
    /// <summary>
    /// Tests if the drop fails because the randomizer returns a number which causes a fail.
    /// </summary>
    [Test]
    public async ValueTask TestDropFailAsync()
    {
        var config = this.GetGameConfig();
        var generator = new DefaultDropGenerator(config, this.GetRandomizer(9999));
        var (items, _) = await generator.GenerateItemDropsAsync(this.GetMonster(1, 0), 0, await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false));
        var item = items.FirstOrDefault();
        Assert.That(item, Is.Null);
    }

    /// <summary>
    /// Tests the drops defined by a monster are getting considered.
    /// </summary>
    [Test]
    public async ValueTask TestItemDropItemByMonsterAsync()
    {
        var config = this.GetGameConfig();
        var monster = this.GetMonster(1, 0);
        monster.DropItemGroups.AddBasicDropItemGroups();
        monster.DropItemGroups.Add(3000, SpecialItemType.RandomItem, true);

        var generator = new DefaultDropGenerator(config, this.GetRandomizer2(0, 0.5));
        var (items, _) = await generator.GenerateItemDropsAsync(monster, 1, await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false));
        var item = items.FirstOrDefault();

        Assert.That(item, Is.Not.Null);

        // ReSharper disable once PossibleNullReferenceException
        Assert.That(item!.Definition, Is.EqualTo(monster.DropItemGroups.Last().PossibleItems.First()));
    }

    /// <summary>
    /// Tests that the drops of a monster which asks for resets only apply to players with enough resets.
    /// </summary>
    /// <param name="playerResets">The resets of the player.</param>
    /// <param name="expectsDrop">If set to <c>true</c>, the monster drops its item.</param>
    [TestCase(4, false)]
    [TestCase(5, true)]
    [TestCase(6, true)]
    public async ValueTask TestDropsOfMonsterRequireResetsAsync(int playerResets, bool expectsDrop)
    {
        var config = this.GetGameConfig();
        var monster = this.GetMonster(1, 0);
        monster.Attributes.Add(new MonsterAttribute { AttributeDefinition = Stats.Resets, Value = 5 });
        monster.DropItemGroups.AddBasicDropItemGroups();
        monster.DropItemGroups.Add(3000, SpecialItemType.RandomItem, true);

        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.Attributes![Stats.Resets] = playerResets;

        var generator = new DefaultDropGenerator(config, this.GetRandomizer2(0, 0.5));
        var (items, _) = await generator.GenerateItemDropsAsync(monster, 1, player).ConfigureAwait(false);

        Assert.That(items.Any(), Is.EqualTo(expectsDrop));
    }

    /// <summary>
    /// Tests that items with a maximum drop level are filtered from generic monster drops.
    /// </summary>
    [Test]
    public async ValueTask TestMaximumDropLevelAsync()
    {
        var config = this.GetGameConfig();
        var cappedItem = this.CreateItemDefinition(12, 15, 12, 66);
        var uncappedItem = this.CreateItemDefinition(14, 13, 25);

        var dropGroup = new Mock<DropItemGroup>();
        dropGroup.SetupAllProperties();
        dropGroup.Object.Chance = 1.0;
        dropGroup.Object.ItemType = SpecialItemType.Jewel;
        dropGroup.Setup(g => g.PossibleItems).Returns(new List<ItemDefinition> { cappedItem, uncappedItem });

        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.CurrentMap!.Definition.DropItemGroups.Add(dropGroup.Object);

        var generator = new DefaultDropGenerator(config, this.GetRandomizer(0));
        var (items, _) = await generator.GenerateItemDropsAsync(this.GetMonster(1, 67), 1, player).ConfigureAwait(false);
        var item = items.FirstOrDefault();

        Assert.That(item, Is.Not.Null);
        Assert.That(item!.Definition, Is.EqualTo(uncappedItem));
    }

    /// <summary>
    /// Tests the drops defined by a player are getting considered.
    /// </summary>
    public void TestItemDropItemByPlayer()
    {
        // to be implemented
    }

    /// <summary>
    /// Tests the drops defined by a map are getting considered.
    /// </summary>
    public void TestItemDropItemByMap()
    {
        // to be implemented
    }

    /// <summary>
    /// Tests that ExcellentItemDropLevelDelta property exists and has correct default.
    /// </summary>
    [Test]
    public void TestExcellentItemDropLevelDelta_PropertyExists()
    {
        var config = this.GetGameConfig();
        // The initializer sets default to 25 for backward compatibility
        config.ExcellentItemDropLevelDelta = 25;
        Assert.That(config.ExcellentItemDropLevelDelta, Is.EqualTo(25));

        config.ExcellentItemDropLevelDelta = 0;
        Assert.That(config.ExcellentItemDropLevelDelta, Is.EqualTo(0));

        config.ExcellentItemDropLevelDelta = 50;
        Assert.That(config.ExcellentItemDropLevelDelta, Is.EqualTo(50));
    }

    /// <summary>
    /// Tests that the money drop rate of the economy plugin scales the dropped zen.
    /// </summary>
    /// <param name="moneyDropRate">The money drop rate.</param>
    /// <param name="expectedMoney">The expected dropped money.</param>
    [TestCase(1f, 1000u)]
    [TestCase(0.3f, 300u)]
    [TestCase(0f, null)]
    public async ValueTask TestMoneyDropRateAsync(float moneyDropRate, uint? expectedMoney)
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        AddEconomy(player, new EconomyConfiguration { MoneyDropRate = moneyDropRate });
        var monster = this.GetMonster(1, 0);
        monster.DropItemGroups.Add(this.CreateDropGroup(1.0, SpecialItemType.Money));

        var generator = new DefaultDropGenerator(this.GetGameConfig(), this.GetRandomizer2(0, 0.5));
        var (_, money) = await generator.GenerateItemDropsAsync(monster, 993, player).ConfigureAwait(false);

        Assert.That(money, Is.EqualTo(expectedMoney));
    }

    /// <summary>
    /// Tests that offline leveling players get less valuable drops and less zen, and the others don't.
    /// </summary>
    /// <param name="isOffline">If set to <c>true</c>, the killer is offline leveling.</param>
    /// <param name="expectsJewel">If set to <c>true</c>, the jewel is expected to drop.</param>
    /// <param name="expectedMoney">The expected dropped money.</param>
    [TestCase(false, true, 1000u)]
    [TestCase(true, false, 500u)]
    public async ValueTask TestAfkDropRatesAsync(bool isOffline, bool expectsJewel, uint expectedMoney)
    {
        var regularPlayer = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var player = isOffline
            ? await PlayerTestHelper.CreateOfflineLevelingPlayerAsync(regularPlayer.GameContext).ConfigureAwait(false)
            : regularPlayer;
        AddEconomy(player, new EconomyConfiguration { AfkMoneyDropRate = 0.5f, AfkValuableDropRate = 0.3f });
        var jewel = this.CreateItemDefinition(14, 13, 0);
        var monster = this.GetMonster(2, 0);
        monster.DropItemGroups.Add(this.CreateDropGroup(1.0, SpecialItemType.Money));
        monster.DropItemGroups.Add(this.CreateDropGroup(0.5, SpecialItemType.Jewel, jewel));

        // The random value 0.5 selects the jewel group, but is above the AFK rate of 0.3.
        var generator = new DefaultDropGenerator(this.GetGameConfig(), this.GetRandomizer2(0, 0.5));
        var (items, money) = await generator.GenerateItemDropsAsync(monster, 993, player).ConfigureAwait(false);

        Assert.That(items.Any(i => i.Definition == jewel), Is.EqualTo(expectsJewel));
        Assert.That(money, Is.EqualTo(expectedMoney));
    }

    private static void AddEconomy(Player player, EconomyConfiguration configuration)
    {
        player.GameContext.FeaturePlugIns.AddPlugIn(new EconomyFeaturePlugIn { Configuration = configuration }, true);
    }

    private DropItemGroup CreateDropGroup(double chance, SpecialItemType itemType, params ItemDefinition[] items)
    {
        var dropGroup = new Mock<DropItemGroup>();
        dropGroup.SetupAllProperties();
        dropGroup.Object.Chance = chance;
        dropGroup.Object.ItemType = itemType;
        dropGroup.Setup(g => g.PossibleItems).Returns(items.ToList());
        return dropGroup.Object;
    }

    private MonsterDefinition GetMonster(int numberOfDrops, byte level)
    {
        var monster = new Mock<MonsterDefinition>();
        monster.SetupAllProperties();
        monster.Setup(m => m.DropItemGroups).Returns(new List<DropItemGroup>());
        monster.Setup(m => m.Attributes).Returns(new List<MonsterAttribute>());
        monster.Object.NumberOfMaximumItemDrops = numberOfDrops;
        monster.Object.Attributes.Add(new MonsterAttribute { AttributeDefinition = Stats.Level, Value = level });
        return monster.Object;
    }

    private IRandomizer GetRandomizer(int randomValue)
    {
        var randomizer = new Mock<IRandomizer>();
        randomizer.Setup(r => r.NextInt(It.IsAny<int>(), It.IsAny<int>())).Returns(randomValue);
        randomizer.Setup(r => r.NextDouble()).Returns(randomValue / 10000.0);
        return randomizer.Object;
    }

    private IRandomizer GetRandomizer2(int integerValue, double doubleValue)
    {
        var randomizer = new Mock<IRandomizer>();
        randomizer.Setup(r => r.NextInt(It.IsAny<int>(), It.IsAny<int>())).Returns(integerValue);
        randomizer.Setup(r => r.NextDouble()).Returns(doubleValue);

        return randomizer.Object;
    }

    private GameConfiguration GetGameConfig()
    {
        var gameConfiguration = new Mock<GameConfiguration>();
        gameConfiguration.Setup(c => c.Items).Returns(new List<ItemDefinition>());
        return gameConfiguration.Object;
    }

    private ItemDefinition CreateItemDefinition(byte group, short number, byte dropLevel, byte? maximumDropLevel = null)
    {
        var itemDefinition = new Mock<ItemDefinition>();
        itemDefinition.SetupAllProperties();
        itemDefinition.Object.Group = group;
        itemDefinition.Object.Number = number;
        itemDefinition.Object.DropLevel = dropLevel;
        itemDefinition.Object.MaximumDropLevel = maximumDropLevel;
        itemDefinition.Object.Durability = 1;
        itemDefinition.Setup(d => d.PossibleItemOptions).Returns(new List<ItemOptionDefinition>());
        return itemDefinition.Object;
    }
}
