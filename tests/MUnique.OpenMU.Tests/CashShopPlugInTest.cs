// <copyright file="CashShopPlugInTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using System.Text;
using Moq;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.GameLogic.PlugIns.CashShop;
using MUnique.OpenMU.GameLogic.PlugIns.Cosmetics;
using MUnique.OpenMU.GameLogic.Views.CashShop;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.CashShop;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.Progression;
using CharacterClass = MUnique.OpenMU.Persistence.BasicModel.CharacterClass;
using IncreasableItemOption = MUnique.OpenMU.Persistence.BasicModel.IncreasableItemOption;
using ItemDefinition = MUnique.OpenMU.Persistence.BasicModel.ItemDefinition;
using ItemOptionDefinition = MUnique.OpenMU.Persistence.BasicModel.ItemOptionDefinition;

/// <summary>
/// Tests for the cash shop: <see cref="CashShopCatalog"/>, <see cref="CashShopCatalogFiles"/>,
/// <see cref="CashShopPlugIn"/> and <see cref="CashShopExampleCatalog"/>.
/// </summary>
[TestFixture]
public class CashShopPlugInTest
{
    private const byte SwordGroup = 0;
    private const short SwordNumber = 5;
    private const byte HelmGroup = 7;
    private const short HelmNumber = 2;
    private const byte JewelGroup = 12;
    private const short JewelNumber = 30;
    private const short SkinSetNumber = 55;

    private const int SwordProduct = 101;
    private const int HelmProduct = 102;
    private const int FixedHelmProduct = 103;
    private const int JewelProduct = 1001;
    private const int SkinProduct = 1201;

    /// <summary>
    /// Tests that the tabs are shown in the order of their first product, with their categories.
    /// </summary>
    [Test]
    public void ProductsAreShownInTabsAndCategories()
    {
        var catalog = CashShopCatalog.Build(CreateConfiguration(), CreateItems());

        var tabs = catalog.Categories.Where(c => c.ParentSeq is null).ToList();
        Assert.That(tabs.Select(t => t.Name), Is.EqualTo(new[] { "DK", "Extras" }));
        Assert.That(tabs.Select(t => t.Seq), Is.EqualTo(new[] { 1, 2 }));

        var categories = catalog.Categories.Where(c => c.ParentSeq is not null).ToDictionary(c => c.Seq);
        string PathOf(int productNumber)
        {
            var category = categories[catalog.GetPackage(productNumber)!.CategorySeq];
            return $"{tabs.Single(t => t.Seq == category.ParentSeq).Name}/{category.Name}";
        }

        Assert.That(PathOf(SwordProduct), Is.EqualTo("DK/Armas"));
        Assert.That(PathOf(HelmProduct), Is.EqualTo("DK/Set"));
        Assert.That(PathOf(FixedHelmProduct), Is.EqualTo("DK/Set"), "The same category name is one category, without regard to case");
        Assert.That(PathOf(JewelProduct), Is.EqualTo("Extras/Joyas"));
        Assert.That(PathOf(SkinProduct), Is.EqualTo("Extras/Skins"));
    }

    /// <summary>
    /// Tests that the player chooses the excellent options from a variant per combination, in addition to the fixed ones.
    /// </summary>
    [Test]
    public void ExcellentOptionChoicesAreVariants()
    {
        var catalog = CashShopCatalog.Build(CreateConfiguration(), CreateItems());

        var sword = catalog.GetPackage(SwordProduct)!;
        Assert.That(sword.Name, Is.EqualTo("Sword +13"));
        Assert.That(sword.ItemCode, Is.EqualTo((SwordGroup * 512) + SwordNumber));

        // 6 options, 2 of them: 15 combinations, with distinct masks of 2 bits.
        Assert.That(sword.Variants, Has.Count.EqualTo(15));
        Assert.That(sword.Variants.Select(v => v.Item.ExcellentOptions).Distinct().Count(), Is.EqualTo(15));
        Assert.That(sword.Variants.All(v => BitCount(v.Item.ExcellentOptions) == 2), Is.True);
        Assert.That(sword.Variants.All(v => v.Price == 1000 && v.Item.Level == 13 && v.Item.Luck && v.Item.Skill), Is.True);
        Assert.That(sword.Variants.Select(v => v.PriceSeq), Is.EqualTo(Enumerable.Range((SwordProduct * 100) + 1, 15)));
        Assert.That(sword.Description, Is.EqualTo("+13, Skill, Luck. Elegí 2 opciones excelentes al comprar."));

        // The helm has no skill, so its items don't get one.
        Assert.That(catalog.GetPackage(HelmProduct)!.Variants.All(v => !v.Item.Skill), Is.True);

        // A fixed option and one to choose: the 5 other options.
        var fixedHelm = catalog.GetPackage(FixedHelmProduct)!;
        Assert.That(fixedHelm.Variants, Has.Count.EqualTo(5));
        Assert.That(fixedHelm.Variants.All(v => (v.Item.ExcellentOptions & 1) == 1 && BitCount(v.Item.ExcellentOptions) == 2), Is.True);
    }

    /// <summary>
    /// Tests that products which can't be shown are left out: inactive ones, those without item and those with
    /// more options to choose than the item has.
    /// </summary>
    [Test]
    public void InvalidProductsAreLeftOut()
    {
        var configuration = CreateConfiguration();
        configuration.Products.Add(new CashShopProductDefinition { Number = 7, Tab = "DW", Item = null });
        configuration.Products.Add(new CashShopProductDefinition { Number = 8, Tab = "DW", Item = CreateItems()[0], IsActive = false });
        configuration.Products.Add(new CashShopProductDefinition { Number = 9, Tab = "DW", Item = CreateItems()[2], ExcellentOptionChoices = 1 });
        configuration.Products.Add(new CashShopProductDefinition { Number = SwordProduct, Tab = "DW", Item = CreateItems()[2] });

        var catalog = CashShopCatalog.Build(configuration, CreateItems());

        Assert.That(catalog.GetPackage(7), Is.Null);
        Assert.That(catalog.GetPackage(8), Is.Null);
        Assert.That(catalog.GetPackage(9), Is.Null);
        Assert.That(catalog.GetPackage(SwordProduct)!.Name, Is.EqualTo("Sword +13"), "The first product with a number wins");
        Assert.That(catalog.Categories.Any(c => c.Name == "DW"), Is.False, "A tab without products isn't shown");
    }

    /// <summary>
    /// Tests that a product without options to choose is a package with one variant, which the client buys without price number.
    /// </summary>
    [Test]
    public void ProductWithoutChoicesHasOneVariant()
    {
        var catalog = CashShopCatalog.Build(CreateConfiguration(), CreateItems());

        var jewel = catalog.GetPackage(JewelProduct)!;
        Assert.That(jewel.Name, Is.EqualTo("Pack de 10 Bless"));
        Assert.That(jewel.Variants, Has.Count.EqualTo(1));

        var skin = catalog.GetPackage(SkinProduct)!;
        Assert.That(skin.Name, Is.EqualTo("Skin Toranaga Armor"));
        Assert.That(skin.Variants.Single().Item.IsSkin, Is.True);

        Assert.That(catalog.FindVariant(JewelProduct, 0), Is.Not.Null);
        Assert.That(catalog.FindVariant(JewelProduct, jewel.Variants[0].PriceSeq), Is.Not.Null);
        Assert.That(catalog.FindVariant(JewelProduct, 12345), Is.Null);
        Assert.That(catalog.FindVariant(SwordProduct, 0), Is.Null, "A package with several variants needs the price number.");
    }

    /// <summary>
    /// Tests the format of the files, and that their version follows the content.
    /// </summary>
    [Test]
    public void FilesHaveTheFormatOfTheClient()
    {
        var configuration = CreateConfiguration();
        var catalog = CashShopCatalog.Build(configuration, CreateItems());
        var files = CashShopCatalogFiles.Create(catalog, configuration.SalesZone);

        var categoryLines = files.Categories.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.That(categoryLines.All(l => l.Split('@').Length == 7), Is.True);
        Assert.That(categoryLines, Does.Contain("1@DK@0@1@0@1@1"));
        Assert.That(categoryLines, Does.Contain("100@Armas@0@1@1@3@0"));

        var packageLines = files.Packages.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.That(packageLines, Has.Length.EqualTo(catalog.Packages.Count));
        Assert.That(packageLines.All(l => l.Split('@').Length == 27), Is.True);
        var swordFields = packageLines.Single(l => l.Split('@')[1] == $"{SwordProduct}").Split('@');
        Assert.That(swordFields[9], Is.EqualTo("0"), "No gifts");
        Assert.That(swordFields[14], Is.EqualTo("Cyber Coins"));
        Assert.That(swordFields[22], Is.EqualTo("15"));
        Assert.That(swordFields[23].Split('|'), Has.Length.EqualTo(15));

        var productLines = files.Products.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.That(productLines.All(l => l.Split('@').Length == 17), Is.True);
        Assert.That(productLines.Count(l => l.StartsWith($"{SwordProduct}@", StringComparison.Ordinal)), Is.EqualTo(15));

        Assert.That(files.Version, Does.Match(@"^777\.\d{4}\.\d{3}$"));
        Assert.That(CashShopCatalogFiles.Create(catalog, configuration.SalesZone).Version, Is.EqualTo(files.Version));

        configuration.Products.First().Price = 900;
        var changed = CashShopCatalogFiles.Create(CashShopCatalog.Build(configuration, CreateItems()), configuration.SalesZone);
        Assert.That(changed.Version, Is.Not.EqualTo(files.Version));
    }

    /// <summary>
    /// Tests that names can't break the format, and that the files are encoded as Windows-1252.
    /// </summary>
    [Test]
    public void TextIsCleanedAndEncodedAsWindows1252()
    {
        Assert.That(CashShopCatalogFiles.Clean("a@b|c\r\nd"), Is.EqualTo("a b c  d"));
        Assert.That(CashShopCatalogFiles.Encode("Maná"), Is.EqualTo(new byte[] { 0x4D, 0x61, 0x6E, 0xE1 }));
        Assert.That(CashShopCatalogFiles.Encode("é")[0], Is.Not.EqualTo(Encoding.UTF8.GetBytes("é")[0]));
    }

    /// <summary>
    /// Tests that buying takes the coins and puts the item into the storage.
    /// </summary>
    [Test]
    public async Task BuyingTakesTheCoinsAsync()
    {
        var repository = new InMemoryCashShopRepository();
        var plugIn = CreatePlugIn(repository);
        var player = await CreatePlayerAsync().ConfigureAwait(false);
        var accountId = player.Account!.GetId();
        await repository.AddPointsAsync(accountId, 1500, "test").ConfigureAwait(false);
        var variant = plugIn.GetCatalog(player.GameContext).Catalog.GetPackage(SwordProduct)!.Variants[3];

        Assert.That(await plugIn.BuyAsync(player, SwordProduct, variant.PriceSeq).ConfigureAwait(false), Is.EqualTo(CashShopBuyResult.Success));

        Assert.That(await repository.GetPointsAsync(accountId).ConfigureAwait(false), Is.EqualTo(500));
        var (items, total) = await repository.LoadStorageAsync(accountId, 0, 10).ConfigureAwait(false);
        Assert.That(total, Is.EqualTo(1));
        Assert.That(items[0].ExcellentOptions, Is.EqualTo(variant.Item.ExcellentOptions));
        Assert.That(items[0].ItemLevel, Is.EqualTo(13));
        Assert.That(items[0].Price, Is.EqualTo(1000));
        Assert.That(items[0].IsSkin, Is.False);

        Assert.That(await plugIn.BuyAsync(player, SwordProduct, variant.PriceSeq).ConfigureAwait(false), Is.EqualTo(CashShopBuyResult.NotEnoughPoints));
        Assert.That(await repository.GetPointsAsync(accountId).ConfigureAwait(false), Is.EqualTo(500));
        Assert.That(await plugIn.BuyAsync(player, SwordProduct, 1).ConfigureAwait(false), Is.EqualTo(CashShopBuyResult.NotAvailable));
    }

    /// <summary>
    /// Tests that nothing can be bought when the storage is full.
    /// </summary>
    [Test]
    public async Task FullStorageRefusesPurchasesAsync()
    {
        var repository = new InMemoryCashShopRepository();
        var plugIn = CreatePlugIn(repository);
        plugIn.Configuration!.MaximumStorageItems = 1;
        var player = await CreatePlayerAsync().ConfigureAwait(false);
        var accountId = player.Account!.GetId();
        await repository.AddPointsAsync(accountId, 1000, "test").ConfigureAwait(false);

        Assert.That(await plugIn.BuyAsync(player, JewelProduct, 0).ConfigureAwait(false), Is.EqualTo(CashShopBuyResult.Success));
        Assert.That(await plugIn.BuyAsync(player, JewelProduct, 0).ConfigureAwait(false), Is.EqualTo(CashShopBuyResult.StorageFull));
        Assert.That(await repository.GetPointsAsync(accountId).ConfigureAwait(false), Is.EqualTo(900));
    }

    /// <summary>
    /// Tests that using a bought item puts it into the inventory once.
    /// </summary>
    [Test]
    public async Task UsingAStorageItemPutsItIntoTheInventoryOnceAsync()
    {
        var repository = new InMemoryCashShopRepository();
        var plugIn = CreatePlugIn(repository);
        var player = await CreatePlayerAsync().ConfigureAwait(false);
        var accountId = player.Account!.GetId();
        await repository.AddPointsAsync(accountId, 1000, "test").ConfigureAwait(false);
        var sword = plugIn.GetCatalog(player.GameContext).Catalog.GetPackage(SwordProduct)!;
        await plugIn.BuyAsync(player, SwordProduct, sword.Variants[0].PriceSeq).ConfigureAwait(false);
        var stored = (await repository.LoadStorageAsync(accountId, 0, 10).ConfigureAwait(false)).Items.Single();

        Assert.That(await plugIn.ConsumeAsync(player, stored.Id).ConfigureAwait(false), Is.EqualTo(CashShopConsumeResult.Success));

        var item = player.Inventory!.Items.Single();
        Assert.That(item.Definition!.Number, Is.EqualTo(SwordNumber));
        Assert.That(item.Level, Is.EqualTo(13));
        Assert.That(item.HasSkill, Is.True);
        Assert.That(item.ItemOptions.Count(o => o.ItemOption!.OptionType == ItemOptionTypes.Excellent), Is.EqualTo(2));
        Assert.That(item.ItemOptions.Any(o => o.ItemOption!.OptionType == ItemOptionTypes.Luck), Is.True);
        Assert.That(item.SocketCount, Is.EqualTo(0));

        Assert.That(await plugIn.ConsumeAsync(player, stored.Id).ConfigureAwait(false), Is.EqualTo(CashShopConsumeResult.NotFound));
        Assert.That(player.Inventory.Items.Count(), Is.EqualTo(1));
        Assert.That((await repository.LoadStorageAsync(accountId, 0, 10).ConfigureAwait(false)).TotalCount, Is.EqualTo(0));
    }

    /// <summary>
    /// Tests that using a bought skin unlocks all pieces of the set for the character, without an item,
    /// and that a character which has the skin already can't use it.
    /// </summary>
    [Test]
    public async Task UsingASkinUnlocksItsPiecesAsync()
    {
        var repository = new InMemoryCashShopRepository();
        var plugIn = CreatePlugIn(repository);
        var player = await CreatePlayerAsync().ConfigureAwait(false);
        var cosmetics = new CosmeticsPlugIn(new InMemoryProgressionRepository()) { Configuration = CreateCosmetics() };
        player.GameContext.PlugInManager.RegisterPlugInAtPlugInPoint<IPlayerStateChangedPlugIn>(cosmetics);
        var accountId = player.Account!.GetId();
        await repository.AddPointsAsync(accountId, 1000, "test").ConfigureAwait(false);
        await plugIn.BuyAsync(player, SkinProduct, 0).ConfigureAwait(false);
        await plugIn.BuyAsync(player, SkinProduct, 0).ConfigureAwait(false);
        var stored = (await repository.LoadStorageAsync(accountId, 0, 10).ConfigureAwait(false)).Items;
        Assert.That(stored.All(s => s.IsSkin), Is.True);

        Assert.That(await plugIn.ConsumeAsync(player, stored[0].Id).ConfigureAwait(false), Is.EqualTo(CashShopConsumeResult.Success));

        var unlocked = (await cosmetics.GetPiecesAsync(player).ConfigureAwait(false))!.Where(p => p.IsUnlocked).Select(p => p.Definition.Number);
        Assert.That(unlocked, Is.EquivalentTo(new short[] { 551, 552, 553, 554, 555 }));
        Assert.That(player.Inventory!.Items, Is.Empty);

        Assert.That(await plugIn.ConsumeAsync(player, stored[1].Id).ConfigureAwait(false), Is.EqualTo(CashShopConsumeResult.CannotUse));
        Assert.That((await repository.LoadStorageAsync(accountId, 0, 10).ConfigureAwait(false)).TotalCount, Is.EqualTo(1), "The second skin stays in the storage");
    }

    /// <summary>
    /// Tests that a skin stays in the storage when the skins are not available.
    /// </summary>
    [Test]
    public async Task SkinWithoutCosmeticsStaysInTheStorageAsync()
    {
        var repository = new InMemoryCashShopRepository();
        var plugIn = CreatePlugIn(repository);
        var player = await CreatePlayerAsync().ConfigureAwait(false);
        var accountId = player.Account!.GetId();
        await repository.AddPointsAsync(accountId, 1000, "test").ConfigureAwait(false);
        await plugIn.BuyAsync(player, SkinProduct, 0).ConfigureAwait(false);
        var stored = (await repository.LoadStorageAsync(accountId, 0, 10).ConfigureAwait(false)).Items.Single();

        Assert.That(await plugIn.ConsumeAsync(player, stored.Id).ConfigureAwait(false), Is.EqualTo(CashShopConsumeResult.CannotUse));
        Assert.That((await repository.LoadStorageAsync(accountId, 0, 10).ConfigureAwait(false)).TotalCount, Is.EqualTo(1));
    }

    /// <summary>
    /// Tests that the coins of a reference are given only once, and that the balance can't go below zero.
    /// </summary>
    [Test]
    public async Task CoinsOfAReferenceAreGivenOnceAsync()
    {
        var repository = new InMemoryCashShopRepository();
        var accountId = Guid.NewGuid();

        Assert.That(await repository.AddPointsAsync(accountId, 100, "referral", "referral:1").ConfigureAwait(false), Is.EqualTo(100));
        Assert.That(await repository.AddPointsAsync(accountId, 100, "referral", "referral:1").ConfigureAwait(false), Is.Null);
        Assert.That(await repository.AddPointsAsync(accountId, -150, "gm").ConfigureAwait(false), Is.Null);
        Assert.That(await repository.GetPointsAsync(accountId).ConfigureAwait(false), Is.EqualTo(100));
        Assert.That(repository.Transactions, Has.Count.EqualTo(1));
    }

    /// <summary>
    /// Tests that the example catalog has tiers of weapons and armor sets without sockets per class, +9 with luck,
    /// where the best ones are the most expensive, and the extras.
    /// </summary>
    [Test]
    public void ExampleCatalogHasTiersOfItemsPerClass()
    {
        var knight = new CharacterClass { Number = 7, Name = "Blade Master" };
        var wizard = new CharacterClass { Number = 0, Name = "Dark Wizard" };
        var items = new List<ItemDefinition>
        {
            WithClasses(CreateExcellentItem(0, 1, "Old Sword", true, dropLevel: 50), knight),
            WithClasses(CreateExcellentItem(0, 2, "Best Sword", true, dropLevel: 100), knight),
            WithClasses(CreateExcellentItem(0, 3, "Socket Sword", true, dropLevel: 150, sockets: 3), knight),
            WithClasses(CreateExcellentItem(5, 1, "Staff", true, dropLevel: 90), wizard),
            (ItemDefinition)CreateItems()[2],
        };
        for (byte group = 7; group <= 11; group++)
        {
            items.Add(WithClasses(CreateExcellentItem(group, 10, $"Old {group}", false, dropLevel: 60), knight));
            items.Add(WithClasses(CreateExcellentItem(group, 20, $"Best {group}", false, dropLevel: 120), knight));
            items.Add(WithClasses(CreateExcellentItem(group, 30, $"Socket {group}", false, dropLevel: 200, sockets: 2), knight));
        }

        var gameConfiguration = new Mock<DataModel.Configuration.GameConfiguration>();
        gameConfiguration.Setup(c => c.Items).Returns(items.Cast<DataModel.Configuration.Items.ItemDefinition>().ToList());

        var configuration = CashShopExampleCatalog.Create(gameConfiguration.Object, null);

        var dk = configuration.Products.Where(p => p.Tab == "DK").ToList();
        Assert.That(dk.Select(p => p.Item!.Name.Value), Is.EqualTo(new[] { "Old Sword", "Best Sword", "Old 7", "Old 8", "Old 9", "Old 10", "Old 11", "Best 7", "Best 8", "Best 9", "Best 10", "Best 11" }));
        Assert.That(dk.All(p => p is { ItemLevel: 9, Luck: true }), Is.True);
        Assert.That(dk.Select(p => p.Category).Distinct(), Is.EqualTo(new[] { "Armas", "Sets" }));

        // Two items get the two strongest tiers: the best one is much more expensive and has two excellent options.
        Assert.That(dk.Where(p => p.Category == "Armas").Select(p => (p.Price, p.ExcellentOptionChoices)), Is.EqualTo(new[] { (800, (byte)1), (2000, (byte)2) }));
        Assert.That(dk.Single(p => p.Item!.Name.Value == "Best 8").Price, Is.GreaterThan(2 * dk.Single(p => p.Item!.Name.Value == "Old 8").Price));
        Assert.That(configuration.Products.Where(p => p.Tab == "DW").Select(p => p.Item!.Name.Value), Is.EqualTo(new[] { "Staff" }));
        Assert.That(configuration.Products.Single(p => p.Tab == "Extras").Name, Is.EqualTo("Pack de 10 Bless"));
        Assert.That(configuration.Products.Select(p => p.Number), Is.Unique);

        var catalog = CashShopCatalog.Build(configuration, gameConfiguration.Object.Items);
        Assert.That(catalog.Categories.Where(c => c.ParentSeq is null).Select(c => c.Name), Is.EqualTo(new[] { "DK", "DW", "Extras" }));
    }

    /// <summary>
    /// Tests that the update only fills a cash shop without products.
    /// </summary>
    [Test]
    public void UpdateRecognizesConfiguredProducts()
    {
        Assert.That(AddCashShopExampleCatalogPlugIn.HasProducts(null), Is.False);
        Assert.That(AddCashShopExampleCatalogPlugIn.HasProducts("""{"CurrencyName":"Cyber Coins","Products":[]}"""), Is.False);
        Assert.That(AddCashShopExampleCatalogPlugIn.HasProducts("""{"$id":"","Products":{"$id":"","$values":[]}}"""), Is.False);
        Assert.That(AddCashShopExampleCatalogPlugIn.HasProducts("""{"$id":"","Products":{"$id":"","$values":[{"Number":1}]}}"""), Is.True);
        Assert.That(AddCashShopExampleCatalogPlugIn.HasProducts("""{"Products":[{"Number":1}]}"""), Is.True);
    }

    /// <summary>
    /// Tests that the update reads the configuration of the cosmetics like it's stored in the database:
    /// with preserved references, where all ids are empty.
    /// </summary>
    [Test]
    public void UpdateReadsStoredCosmetics()
    {
        var stored = new MUnique.OpenMU.PlugIns.PlugInConfiguration
        {
            CustomConfiguration = """
                {
                  "$id": "",
                  "RestrictToQualifiedClasses": true,
                  "Pieces": {
                    "$id": "",
                    "$values": [
                      { "$id": "", "Number": 11, "Slot": 0, "ItemNumber": 1, "Level": 9 },
                      { "$id": "", "Number": 12, "Slot": 1, "ItemNumber": 1, "Level": 9 }
                    ]
                  }
                }
                """,
        };

        var cosmetics = AddCashShopExampleCatalogPlugIn.ReadCosmetics(stored);

        Assert.That(cosmetics?.Pieces.Select(p => p.Number), Is.EqualTo(new short[] { 11, 12 }));
        Assert.That(AddCashShopExampleCatalogPlugIn.ReadCosmetics(null), Is.Null);
    }

    private static int BitCount(byte value) => System.Numerics.BitOperations.PopCount(value);

    private static CashShopConfiguration CreateConfiguration()
    {
        var items = CreateItems();
        var configuration = new CashShopConfiguration();
        configuration.Products.Add(new CashShopProductDefinition { Number = SwordProduct, Tab = "DK", Category = "Armas", Item = items[0], ItemLevel = 13, Skill = true, Luck = true, ExcellentOptionChoices = 2, Price = 1000 });
        configuration.Products.Add(new CashShopProductDefinition { Number = JewelProduct, Tab = "Extras", Category = "Joyas", Name = "Pack de 10 Bless", Item = items[2], Price = 100 });
        configuration.Products.Add(new CashShopProductDefinition { Number = HelmProduct, Tab = "dk", Category = "Set", Item = items[1], ItemLevel = 13, Skill = true, Luck = true, ExcellentOptionChoices = 2, Price = 800 });
        configuration.Products.Add(new CashShopProductDefinition { Number = FixedHelmProduct, Tab = "DK", Category = "set", Item = items[1], ExcellentOptions = 1, ExcellentOptionChoices = 1, Price = 500 });
        configuration.Products.Add(new CashShopProductDefinition { Number = SkinProduct, Tab = "Extras", Category = "Skins", Type = CashShopProductType.Skin, Item = items[3], Price = 500 });
        return configuration;
    }

    private static CosmeticsConfiguration CreateCosmetics()
    {
        var configuration = new CosmeticsConfiguration { RestrictToQualifiedClasses = false };
        foreach (var slot in CosmeticSlotExtensions.ArmorSlots)
        {
            configuration.Pieces.Add(new CosmeticPieceDefinition { Number = (short)((SkinSetNumber * 10) + (int)slot + 1), Slot = slot, ItemNumber = SkinSetNumber });
        }

        // Another set and wings with the same number, which the skin doesn't unlock.
        configuration.Pieces.Add(new CosmeticPieceDefinition { Number = 661, Slot = CosmeticSlot.Helm, ItemNumber = 66 });
        configuration.Pieces.Add(new CosmeticPieceDefinition { Number = 12055, Slot = CosmeticSlot.Wings, ItemNumber = SkinSetNumber });
        return configuration;
    }

    private static CashShopPlugIn CreatePlugIn(ICashShopRepository repository)
    {
        return new CashShopPlugIn(repository) { Configuration = CreateConfiguration() };
    }

    private static List<DataModel.Configuration.Items.ItemDefinition> CreateItems()
    {
        return
        [
            CreateExcellentItem(SwordGroup, SwordNumber, "Sword", withSkill: true),
            CreateExcellentItem(HelmGroup, HelmNumber, "Helm", withSkill: false),
            new ItemDefinition { Group = JewelGroup, Number = JewelNumber, Name = "Packed Jewel of Bless", Width = 1, Height = 1, Durability = 1 },
            new ItemDefinition { Group = 8, Number = SkinSetNumber, Name = "Toranaga Armor", Width = 2, Height = 3 },
        ];
    }

    private static ItemDefinition CreateExcellentItem(byte group, short number, string name, bool withSkill, byte dropLevel = 0, int sockets = 0)
    {
        var options = new ItemOptionDefinition();
        options.PossibleOptions.Add(new IncreasableItemOption { OptionType = ItemOptionTypes.Luck, Number = 0 });
        for (short i = 1; i <= 6; i++)
        {
            options.PossibleOptions.Add(new IncreasableItemOption { OptionType = ItemOptionTypes.Excellent, Number = i });
        }

        var item = new ItemDefinition
        {
            Group = group,
            Number = number,
            Name = name,
            Width = 1,
            Height = 2,
            Durability = 30,
            MaximumItemLevel = 15,
            DropLevel = dropLevel,
            MaximumSockets = sockets,
            Skill = withSkill ? new MUnique.OpenMU.Persistence.BasicModel.Skill { Number = 20 } : null,
        };
        item.PossibleItemOptions.Add(options);
        return item;
    }

    private static ItemDefinition WithClasses(ItemDefinition item, params CharacterClass[] classes)
    {
        foreach (var characterClass in classes)
        {
            item.QualifiedCharacters.Add(characterClass);
        }

        return item;
    }

    private static async ValueTask<Player> CreatePlayerAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        foreach (var item in CreateItems())
        {
            player.GameContext.Configuration.Items.Add(item);
        }

        await player.PlayerState.TryAdvanceToAsync(PlayerState.EnteredWorld).ConfigureAwait(false);
        return player;
    }
}
