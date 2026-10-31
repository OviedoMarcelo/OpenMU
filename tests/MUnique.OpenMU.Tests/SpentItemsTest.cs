// <copyright file="SpentItemsTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Moq;
using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlayerActions.ItemConsumeActions;
using MUnique.OpenMU.GameLogic.Views.Inventory;

/// <summary>
/// Tests <see cref="SpentItems"/>: used up items (e.g. a consumed jewel whose deletion didn't reach
/// the database) are removed instead of blocking the consumption.
/// </summary>
[TestFixture]
public class SpentItemsTest
{
    private const byte JewelSlot = 12;

    /// <summary>
    /// An item which can't be worn counts as used up when it has no pieces left.
    /// </summary>
    /// <param name="definitionDurability">The durability of the item definition, i.e. the pieces of a full item.</param>
    /// <param name="durability">The durability of the item, i.e. its pieces.</param>
    /// <param name="wearable">If set to <c>true</c>, the item can be worn.</param>
    /// <param name="expected">The expected result.</param>
    [TestCase(1, 0, false, true)]
    [TestCase(255, 0, false, true)]
    [TestCase(1, 1, false, false)]
    [TestCase(255, 3, false, false)]
    [TestCase(0, 0, false, false)]
    [TestCase(30, 0, true, false)]
    public void IsSpent(byte definitionDurability, double durability, bool wearable, bool expected)
    {
        var item = CreateItem(definitionDurability, durability, wearable);

        Assert.That(SpentItems.IsSpent(item), Is.EqualTo(expected));
    }

    /// <summary>
    /// Only the used up items of a storage are removed; a broken equipment item stays.
    /// </summary>
    [Test]
    public async ValueTask RemoveFromStorageRemovesOnlySpentItemsAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var storage = player.PersistenceContext.CreateNew<ItemStorage>();
        Item CreateStoredItem(byte definitionDurability, double durability, bool wearable)
        {
            var template = CreateItem(definitionDurability, durability, wearable);
            var item = player.PersistenceContext.CreateNew<Item>();
            item.Definition = template.Definition;
            item.Durability = template.Durability;
            return item;
        }

        var spentJewel = CreateStoredItem(1, 0, false);
        var jewel = CreateStoredItem(1, 1, false);
        var brokenArmor = CreateStoredItem(30, 0, true);
        storage.Items.Add(spentJewel);
        storage.Items.Add(jewel);
        storage.Items.Add(brokenArmor);

        var removed = await SpentItems.RemoveFromStorageAsync(storage, player.PersistenceContext, player.Logger, "test").ConfigureAwait(false);

        Assert.That(removed, Is.EqualTo(1));
        Assert.That(storage.Items, Is.EquivalentTo(new[] { jewel, brokenArmor }));
    }

    /// <summary>
    /// Using a used up Jewel of Bless removes it from the inventory (which also tells the client),
    /// instead of failing every time.
    /// </summary>
    [Test]
    public async ValueTask ConsumingSpentJewelRemovesItAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var bless = CreateItem(1, 0, false);
        bless.Definition!.Group = ItemConstants.JewelOfBless.Group;
        bless.Definition.Number = (short)ItemConstants.JewelOfBless.Number!.Value;
        await player.Inventory!.AddItemAsync(JewelSlot, bless).ConfigureAwait(false);

        await new ItemConsumeAction().HandleConsumeRequestAsync(player, JewelSlot, JewelSlot + 1, FruitUsage.Undefined).ConfigureAwait(false);

        Assert.That(player.Inventory.GetItem(JewelSlot), Is.Null);
        Mock.Get(player.ViewPlugIns.GetPlugIn<IItemRemovedPlugIn>()!).Verify(view => view!.RemoveItemAsync(JewelSlot), Times.Once);
        Mock.Get(player.ViewPlugIns.GetPlugIn<IRequestedItemConsumptionFailedPlugIn>()!).Verify(view => view!.RequestedItemConsumptionFailedAsync(), Times.Never);
    }

    private static Item CreateItem(byte definitionDurability, double durability, bool wearable)
    {
        var definition = new ItemDefinition { Width = 1, Height = 1, Durability = definitionDurability };
        if (wearable)
        {
            var itemSlot = new Mock<ItemSlotType>();
            itemSlot.Setup(s => s.ItemSlots).Returns(new List<int> { InventoryConstants.ArmorSlot });
            definition.ItemSlot = itemSlot.Object;
        }

        return new Item { Definition = definition, Durability = durability };
    }
}
