// <copyright file="StorePricesTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Tests.AdminApi;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.InMemory;
using MUnique.OpenMU.Web.AdminPanel.AdminApi.Configuration;

/// <summary>
/// Tests the overview and the changes of the prices of the merchant stores.
/// </summary>
[TestFixture]
public class StorePricesTests
{
    private IContext _context = null!;
    private GameConfiguration _configuration = null!;
    private ItemDefinition _potion = null!;
    private MonsterDefinition _potionGirl = null!;
    private MonsterDefinition _wizard = null!;

    /// <summary>
    /// Sets up a configuration with two merchants which sell the same potion.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        this._context = new InMemoryPersistenceContextProvider().CreateNewContext();
        this._configuration = this._context.CreateNew<GameConfiguration>();
        this._potion = this._context.CreateNew<ItemDefinition>();
        this._potion.Name = new LocalizedString("Healing Potion");
        this._potion.Group = 14;
        this._potion.Number = 3;
        this._potion.Value = 100;
        this._potion.Durability = 1;
        this._configuration.Items.Add(this._potion);

        this._potionGirl = this.CreateMerchant(253, "Potion Girl");
        this._wizard = this.CreateMerchant(251, "Wizard");
        this.AddToStore(this._potionGirl, 0, 0);
        this.AddToStore(this._wizard, 5, 0, 1000);
        this.AddToStore(this._wizard, 6, 1);
    }

    /// <summary>
    /// Disposes the context.
    /// </summary>
    [TearDown]
    public void TearDown()
    {
        this._context.Dispose();
    }

    /// <summary>
    /// Tests that there is one row per item and level, with the stores which sell it and their prices.
    /// </summary>
    [Test]
    public void ItemsOnSaleAreGroupedByItemAndLevel()
    {
        var items = StorePrices.GetItemsOnSale(this._configuration);

        Assert.That(items.Select(i => i.Level), Is.EqualTo(new[] { 0, 1 }));
        var level0 = items[0];
        Assert.That(level0.Name, Is.EqualTo("Healing Potion"));
        Assert.That(level0.Offers.Select(o => o.MerchantName), Is.EqualTo(new[] { "Wizard", "Potion Girl" }), "Ordered by the number of the merchant.");
        Assert.That(level0.Offers.Select(o => o.StorePrice), Is.EqualTo(new int?[] { 1000, null }));
        Assert.That(level0.Offers.All(o => o.NormalPrice > 0), Is.True, "The normal price is calculated from the item.");
    }

    /// <summary>
    /// Tests that the prices are set and removed, and that only the merchants whose prices change are reported.
    /// </summary>
    [Test]
    public void PricesAreChanged()
    {
        var offers = StorePrices.GetItemsOnSale(this._configuration)[0].Offers;
        var wizardItem = offers.Single(o => o.MerchantName == "Wizard").ItemId;
        var girlItem = offers.Single(o => o.MerchantName == "Potion Girl").ItemId;

        var changed = StorePrices.Apply(this._configuration, [new(wizardItem, 1000), new(girlItem, 1000)]);

        Assert.That(changed.Select(c => c.Merchant), Is.EqualTo(new[] { this._potionGirl }), "The wizard had this price already.");
        var change = changed[0].Changes.Single();
        Assert.That(change.Property, Is.EqualTo("MerchantStore.Items[0].StorePrice"));
        Assert.That((change.Before, change.After), Is.EqualTo(((string?)null, "1000")));
        Assert.That(StorePrices.GetItemsOnSale(this._configuration)[0].Offers.Select(o => o.StorePrice), Is.EqualTo(new int?[] { 1000, 1000 }));

        StorePrices.Apply(this._configuration, [new(wizardItem, null)]);
        Assert.That(this._wizard.MerchantStore!.Items.First(i => i.Level == 0).StorePrice, Is.Null);
    }

    /// <summary>
    /// Tests that nothing is changed when a change is invalid.
    /// </summary>
    [Test]
    public void InvalidChangesChangeNothing()
    {
        var girlItem = StorePrices.GetItemsOnSale(this._configuration)[0].Offers.Single(o => o.MerchantName == "Potion Girl").ItemId;

        var exception = Assert.Throws<ConfigurationValidationException>(() =>
            StorePrices.Apply(this._configuration, [new(girlItem, 500), new(Guid.NewGuid(), 10), new(girlItem, 0)]));

        Assert.That(exception!.Errors.Select(e => e.Path), Is.EqualTo(new[] { "[1].ItemId", "[2].Price" }));
        Assert.That(this._potionGirl.MerchantStore!.Items.Single().StorePrice, Is.Null);
    }

    private MonsterDefinition CreateMerchant(short number, string name)
    {
        var merchant = this._context.CreateNew<MonsterDefinition>();
        merchant.Number = number;
        merchant.Designation = new LocalizedString(name);
        merchant.NpcWindow = NpcWindow.Merchant;
        merchant.MerchantStore = this._context.CreateNew<ItemStorage>();
        this._configuration.Monsters.Add(merchant);
        return merchant;
    }

    private void AddToStore(MonsterDefinition merchant, byte slot, byte level, int? storePrice = null)
    {
        var item = this._context.CreateNew<Item>();
        item.Definition = this._potion;
        item.ItemSlot = slot;
        item.Level = level;
        item.Durability = 1;
        item.StorePrice = storePrice;
        merchant.MerchantStore!.Items.Add(item);
    }
}
