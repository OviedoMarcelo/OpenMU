// <copyright file="MerchantStorePricesTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Moq;
using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.PlayerActions.Items;

/// <summary>
/// Tests that the price which a merchant store sets for an item (<see cref="Item.StorePrice"/>)
/// is asked when buying it and pays a third of it when selling it, see <see cref="MerchantStorePrices"/>.
/// </summary>
[TestFixture]
public class MerchantStorePricesTests
{
    private const byte StoreSlot = 0;
    private const byte InventorySlot = 20;
    private const int StorePrice = 1_000_000;

    /// <summary>
    /// The item is bought for the price of the store, not the calculated one.
    /// </summary>
    [Test]
    public async ValueTask ItemIsBoughtForThePriceOfTheStoreAsync()
    {
        var definition = CreateDefinition();
        var player = await CreatePlayerWithMerchantAsync(CreateItem(definition, 1, StorePrice)).ConfigureAwait(false);
        player.Money = 2_500_000;

        await new BuyNpcItemAction().BuyItemAsync(player, StoreSlot).ConfigureAwait(false);

        Assert.That(player.Money, Is.EqualTo(1_500_000));
        Assert.That(player.Inventory!.Items.Single().Level, Is.EqualTo(1));
    }

    /// <summary>
    /// Without enough money for the price of the store, nothing is bought.
    /// </summary>
    [Test]
    public async ValueTask ItemIsNotBoughtWithoutEnoughMoneyForThePriceOfTheStoreAsync()
    {
        var definition = CreateDefinition();
        var player = await CreatePlayerWithMerchantAsync(CreateItem(definition, 1, StorePrice)).ConfigureAwait(false);
        player.Money = StorePrice - 1;

        await new BuyNpcItemAction().BuyItemAsync(player, StoreSlot).ConfigureAwait(false);

        Assert.That(player.Money, Is.EqualTo(StorePrice - 1));
        Assert.That(player.Inventory!.Items, Is.Empty);
    }

    /// <summary>
    /// An item of which a store sets the price is sold for a third of it.
    /// </summary>
    [Test]
    public async ValueTask ItemIsSoldForAThirdOfThePriceOfTheStoreAsync()
    {
        var definition = CreateDefinition();
        var player = await CreatePlayerWithMerchantAsync(CreateItem(definition, 1, StorePrice)).ConfigureAwait(false);
        await player.Inventory!.AddItemAsync(InventorySlot, CreateItem(definition, 1, null)).ConfigureAwait(false);

        var sold = await new SellItemToNpcAction().SellItemAsync(player, InventorySlot).ConfigureAwait(false);

        Assert.That(sold, Is.True);
        Assert.That(player.Money, Is.EqualTo(333_300));
    }

    /// <summary>
    /// The price of the store applies to its level only: another level keeps the calculated price.
    /// </summary>
    [Test]
    public async ValueTask OtherLevelKeepsTheCalculatedPriceAsync()
    {
        var definition = CreateDefinition();
        var player = await CreatePlayerWithMerchantAsync(CreateItem(definition, 1, StorePrice)).ConfigureAwait(false);
        var item = CreateItem(definition, 2, null);
        var calculatedPrice = new ItemPriceCalculator().CalculateSellingPrice(item, item.Durability());
        await player.Inventory!.AddItemAsync(InventorySlot, item).ConfigureAwait(false);

        await new SellItemToNpcAction().SellItemAsync(player, InventorySlot).ConfigureAwait(false);

        Assert.That(player.Money, Is.EqualTo(calculatedPrice));
    }

    /// <summary>
    /// When several stores set a price for the same item and level, the one of the opened merchant counts.
    /// Items without a price of their own have none.
    /// </summary>
    [Test]
    public void PriceOfTheOpenedMerchantCounts()
    {
        var definition = CreateDefinition();
        var other = CreateMerchant(1, CreateItem(definition, 1, 5_000_000));
        var opened = CreateMerchant(2, CreateItem(definition, 1, StorePrice), CreateItem(definition, 3, null));
        var configuration = new Mock<GameConfiguration>();
        configuration.Setup(c => c.Monsters).Returns(new List<MonsterDefinition> { other, opened });

        var prices = MerchantStorePrices.GetPrices(configuration.Object, opened);

        Assert.That(prices, Has.Count.EqualTo(1));
        Assert.That(prices[(definition, 1)], Is.EqualTo(new MerchantStorePrices.StorePrice(StorePrice, false)));
        Assert.That(MerchantStorePrices.GetPrices(configuration.Object, null)[(definition, 1)].Price, Is.EqualTo(5_000_000));
    }

    /// <summary>
    /// The price of an item which stacks is the one of a piece: a stack of the store costs it times its pieces.
    /// </summary>
    [Test]
    public async ValueTask StackIsBoughtForThePriceOfAPieceTimesItsPiecesAsync()
    {
        var storeItem = CreateItem(CreatePotionDefinition(), 0, 900_000, pieces: 3);
        var player = await CreatePlayerWithMerchantAsync(storeItem).ConfigureAwait(false);
        player.Money = 3_000_000;

        await new BuyNpcItemAction().BuyItemAsync(player, StoreSlot).ConfigureAwait(false);

        Assert.That(player.Money, Is.EqualTo(300_000));
        Assert.That(MerchantStorePrices.GetPrices(player.GameContext.Configuration, null).Single().Value.IsPerPiece, Is.True);
    }

    /// <summary>
    /// A stack of an item which stacks is sold for a third of the price of a piece times its pieces.
    /// </summary>
    [Test]
    public async ValueTask StackIsSoldForAThirdOfThePriceOfItsPiecesAsync()
    {
        var definition = CreatePotionDefinition();
        var player = await CreatePlayerWithMerchantAsync(CreateItem(definition, 0, 900_000)).ConfigureAwait(false);
        await player.Inventory!.AddItemAsync(InventorySlot, CreateItem(definition, 0, null, pieces: 3)).ConfigureAwait(false);

        var sold = await new SellItemToNpcAction().SellItemAsync(player, InventorySlot).ConfigureAwait(false);

        Assert.That(sold, Is.True);
        Assert.That(player.Money, Is.EqualTo(900_000));
    }

    private static async ValueTask<Player> CreatePlayerWithMerchantAsync(Item storeItem)
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var merchant = CreateMerchant(1, storeItem);
        Mock.Get(player.GameContext.Configuration).Setup(c => c.Monsters).Returns(new List<MonsterDefinition> { merchant });
        player.GameContext.Configuration.MaximumInventoryMoney = int.MaxValue;
        player.OpenedNpc = new NonPlayerCharacter(null!, merchant, null!);
        return player;
    }

    private static MonsterDefinition CreateMerchant(short number, params Item[] items)
    {
        var store = new Mock<ItemStorage>();
        store.SetupAllProperties();
        store.Setup(s => s.Items).Returns(items.ToList());
        return new MonsterDefinition { Number = number, MerchantStore = store.Object };
    }

    private static ItemDefinition CreateDefinition()
    {
        // Like the Devil's Invitation.
        return new ItemDefinition { Group = 14, Number = 19, Width = 1, Height = 1, Durability = 1 };
    }

    private static ItemDefinition CreatePotionDefinition()
    {
        // Like the Large Healing Potion, which stacks up to 255 pieces.
        return new ItemDefinition { Group = 14, Number = 3, Width = 1, Height = 1, Durability = 255 };
    }

    private static Item CreateItem(ItemDefinition definition, byte level, int? storePrice, byte pieces = 1)
    {
        var item = new Mock<Item>();
        item.SetupAllProperties();
        item.Setup(i => i.ItemOptions).Returns(new List<ItemOptionLink>());
        item.Setup(i => i.ItemSetGroups).Returns(new List<ItemOfItemSet>());
        item.Object.Definition = definition;
        item.Object.Durability = pieces;
        item.Object.Level = level;
        item.Object.ItemSlot = StoreSlot;
        item.Object.StorePrice = storePrice;
        return item.Object;
    }
}
