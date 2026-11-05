// <copyright file="BackupInventoryRestoreTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.PlayerActions;
using MUnique.OpenMU.GameLogic.PlayerActions.Items;
using MUnique.OpenMU.GameLogic.PlayerActions.Trade;
using MUnique.OpenMU.GameLogic.Views.Trade;
using MUnique.OpenMU.Persistence.InMemory;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Tests that leaving the game never resets the inventory to an outdated <see cref="BackupItemStorage"/>,
/// e.g. the snapshot taken when a crafting NPC was opened.
/// </summary>
[TestFixture]
public class BackupInventoryRestoreTests
{
    /// <summary>
    /// An item which was added (e.g. picked up) after the backup was taken stays in the inventory when leaving the game.
    /// </summary>
    [Test]
    public async ValueTask ItemAddedAfterBackupIsKeptWhenLeavingAsync()
    {
        var player = await CreateTestPlayerAsync().ConfigureAwait(false);
        var character = player.SelectedCharacter!;
        var oldItem = CreateItem(CreateDefinition(), 1);
        await player.Inventory!.AddItemAsync(20, oldItem).ConfigureAwait(false);
        player.BackupInventory = new BackupItemStorage(player.Inventory.ItemStorage);

        var pickedUpItem = CreateItem(CreateDefinition(), 1);
        await player.Inventory.AddItemAsync(30, pickedUpItem).ConfigureAwait(false);

        await player.RemoveFromGameAsync().ConfigureAwait(false);

        Assert.That(player.BackupInventory, Is.Null);
        Assert.That(character.Inventory!.Items, Does.Contain(pickedUpItem));
        Assert.That(character.Inventory.Items, Does.Contain(oldItem));
        Assert.That(pickedUpItem.ItemSlot, Is.EqualTo(30));
    }

    /// <summary>
    /// An item which was removed (e.g. a consumed jewel) after the backup was taken doesn't come back when leaving the game.
    /// </summary>
    [Test]
    public async ValueTask ItemRemovedAfterBackupIsNotRestoredWhenLeavingAsync()
    {
        var player = await CreateTestPlayerAsync().ConfigureAwait(false);
        var character = player.SelectedCharacter!;
        var jewel = CreateItem(CreateDefinition(), 1);
        await player.Inventory!.AddItemAsync(20, jewel).ConfigureAwait(false);
        player.BackupInventory = new BackupItemStorage(player.Inventory.ItemStorage);

        await player.Inventory.RemoveItemAsync(jewel).ConfigureAwait(false);

        await player.RemoveFromGameAsync().ConfigureAwait(false);

        Assert.That(character.Inventory!.Items, Does.Not.Contain(jewel));
    }

    /// <summary>
    /// An item which was moved within the inventory after the backup was taken keeps its new slot when leaving the game.
    /// </summary>
    [Test]
    public async ValueTask ItemMovedAfterBackupKeepsItsSlotWhenLeavingAsync()
    {
        var player = await CreateTestPlayerAsync().ConfigureAwait(false);
        var character = player.SelectedCharacter!;
        var item = CreateItem(CreateDefinition(), 1);
        await player.Inventory!.AddItemAsync(20, item).ConfigureAwait(false);
        player.BackupInventory = new BackupItemStorage(player.Inventory.ItemStorage);

        await new MoveItemAction().MoveItemAsync(player, 20, Storages.Inventory, 40, Storages.Inventory).ConfigureAwait(false);

        await player.RemoveFromGameAsync().ConfigureAwait(false);

        Assert.That(character.Inventory!.Items, Does.Contain(item));
        Assert.That(item.ItemSlot, Is.EqualTo(40));
    }

    /// <summary>
    /// Money which was gained after the backup was taken is kept when leaving the game.
    /// </summary>
    [Test]
    public async ValueTask MoneyGainedAfterBackupIsKeptWhenLeavingAsync()
    {
        var player = await CreateTestPlayerAsync().ConfigureAwait(false);
        var character = player.SelectedCharacter!;
        player.Money = 1000;
        player.BackupInventory = new BackupItemStorage(player.Inventory!.ItemStorage);

        player.Money = 5000;

        await player.RemoveFromGameAsync().ConfigureAwait(false);

        Assert.That(character.Inventory!.Money, Is.EqualTo(5000));
    }

    /// <summary>
    /// Items which are still placed in the dialog (temporary storage) are returned to their original
    /// inventory slots when leaving the game, without touching the other items.
    /// </summary>
    [Test]
    public async ValueTask ItemsInTemporaryStorageAreReturnedToTheirOriginalSlotsAsync()
    {
        var player = await CreateTestPlayerAsync().ConfigureAwait(false);
        var character = player.SelectedCharacter!;
        var mixItem = CreateItem(CreateDefinition(), 1);
        await player.Inventory!.AddItemAsync(20, mixItem).ConfigureAwait(false);
        player.BackupInventory = new BackupItemStorage(player.Inventory.ItemStorage);

        await player.Inventory.RemoveItemAsync(mixItem).ConfigureAwait(false);
        Assert.That(await player.TemporaryStorage!.AddItemAsync(0, mixItem).ConfigureAwait(false), Is.True);
        var pickedUpItem = CreateItem(CreateDefinition(), 1);
        await player.Inventory.AddItemAsync(30, pickedUpItem).ConfigureAwait(false);

        await player.RemoveFromGameAsync().ConfigureAwait(false);

        Assert.That(character.Inventory!.Items, Does.Contain(mixItem));
        Assert.That(mixItem.ItemSlot, Is.EqualTo(20));
        Assert.That(character.Inventory.Items, Does.Contain(pickedUpItem));
        Assert.That(pickedUpItem.ItemSlot, Is.EqualTo(30));
    }

    /// <summary>
    /// Items in the temporary storage are returned to another free slot, if their original slot was occupied in the meantime.
    /// </summary>
    [Test]
    public async ValueTask ItemsInTemporaryStorageAreReturnedToAnotherSlotIfTheOriginalIsOccupiedAsync()
    {
        var player = await CreateTestPlayerAsync().ConfigureAwait(false);
        var character = player.SelectedCharacter!;
        var mixItem = CreateItem(CreateDefinition(), 1);
        await player.Inventory!.AddItemAsync(20, mixItem).ConfigureAwait(false);
        player.BackupInventory = new BackupItemStorage(player.Inventory.ItemStorage);

        await player.Inventory.RemoveItemAsync(mixItem).ConfigureAwait(false);
        Assert.That(await player.TemporaryStorage!.AddItemAsync(0, mixItem).ConfigureAwait(false), Is.True);
        var blocker = CreateItem(CreateDefinition(), 1);
        await player.Inventory.AddItemAsync(20, blocker).ConfigureAwait(false);

        await player.RemoveFromGameAsync().ConfigureAwait(false);

        Assert.That(character.Inventory!.Items, Does.Contain(mixItem));
        Assert.That(character.Inventory.Items, Does.Contain(blocker));
        Assert.That(blocker.ItemSlot, Is.EqualTo(20));
        Assert.That(mixItem.ItemSlot, Is.Not.EqualTo(20));
    }

    /// <summary>
    /// Closing an NPC dialog discards the backup which was taken when it was opened.
    /// </summary>
    [Test]
    public async ValueTask ClosingTheNpcDialogDiscardsTheBackupAsync()
    {
        var player = await CreateTestPlayerAsync().ConfigureAwait(false);
        player.OpenedNpc = new NonPlayerCharacter(null!, new MonsterDefinition { NpcWindow = NpcWindow.ChaosMachine }, null!);
        Assert.That(await player.PlayerState.TryAdvanceToAsync(PlayerState.NpcDialogOpened).ConfigureAwait(false), Is.True);
        player.BackupInventory = new BackupItemStorage(player.Inventory!.ItemStorage);

        await new CloseNpcDialogAction().CloseNpcDialogAsync(player).ConfigureAwait(false);

        Assert.That(player.BackupInventory, Is.Null);
    }

    private static async ValueTask<Player> CreateTestPlayerAsync()
    {
        var gameConfig = new Mock<GameConfiguration>();
        gameConfig.SetupAllProperties();
        gameConfig.Setup(c => c.Maps).Returns(new List<GameMapDefinition>());
        gameConfig.Setup(c => c.Items).Returns(new List<ItemDefinition>());
        gameConfig.Setup(c => c.Skills).Returns(new List<Skill>());
        gameConfig.Setup(c => c.PlugInConfigurations).Returns(new List<PlugInConfiguration>());
        gameConfig.Setup(c => c.CharacterClasses).Returns(new List<CharacterClass>());
        gameConfig.Setup(c => c.Attributes).Returns(new List<AttributeDefinition>());
        gameConfig.Setup(c => c.GlobalAttributeCombinations).Returns(new List<AttributeRelationship>());
        gameConfig.Setup(c => c.GlobalBaseAttributeValues).Returns(new List<ConstValueAttribute>
        {
            new(1, Stats.MoneyAmountRate),
        });
        var map = new Mock<GameMapDefinition>();
        map.SetupAllProperties();
        map.Setup(m => m.DropItemGroups).Returns(new List<DropItemGroup>());
        map.Setup(m => m.MonsterSpawns).Returns(new List<MonsterSpawnArea>());
        map.Object.TerrainData = new byte[ushort.MaxValue + 3];
        gameConfig.Object.RecoveryInterval = int.MaxValue;
        gameConfig.Object.Maps.Add(map.Object);

        var mapInitializer = new MapInitializer(gameConfig.Object, new NullLogger<MapInitializer>(), NullDropGenerator.Instance, null);
        var gameContext = new GameContext(
            gameConfig.Object,
            new InMemoryPersistenceContextProvider(),
            mapInitializer,
            new NullLoggerFactory(),
            new PlugInManager(null, new NullLoggerFactory(), null, null),
            NullDropGenerator.Instance,
            new ConfigurationChangeMediator());
        mapInitializer.PlugInManager = gameContext.PlugInManager;
        mapInitializer.PathFinderPool = gameContext.PathFinderPool;
        return await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
    }

    private static ItemDefinition CreateDefinition(byte width = 1, byte height = 1, byte durability = 1)
    {
        return new ItemDefinition
        {
            Width = width,
            Height = height,
            Durability = durability,
        };
    }

    private static Item CreateItem(ItemDefinition definition, double durability, byte level = 0)
    {
        var item = new Mock<Item>();
        item.SetupAllProperties();
        item.Setup(i => i.ItemOptions).Returns(new List<ItemOptionLink>());
        item.Setup(i => i.ItemSetGroups).Returns(new List<ItemOfItemSet>());
        item.Object.Definition = definition;
        item.Object.Durability = durability;
        item.Object.Level = level;
        return item.Object;
    }
}
