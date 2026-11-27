// <copyright file="EconomyForTheWipeDataTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Tests;

using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.GameLogic.PlugIns.CashShop;
using MUnique.OpenMU.GameLogic.PlugIns.Economy;
using MUnique.OpenMU.GameLogic.PlugIns.InvasionEvents;
using MUnique.OpenMU.GameLogic.Resets;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.InMemory;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Tests the update which applies the economy for the wipe on a new season 6 database.
/// </summary>
[TestFixture]
internal class EconomyForTheWipeDataTest
{
    private const short IcarusGuardianNumber = 709;

    private static readonly Guid JewelsGroupId = new(0x200, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    private static readonly Guid ExcellentGroupId = new(0x200, 3, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    private static readonly Guid MiscGroupId = CreateUpdateGroupId(1);

    private GameConfiguration _gameConfiguration = null!;

    private DropItemGroup _oldBoxes = null!;

    /// <summary>
    /// Creates a season 6 database and applies the update twice, like an admin who installs it again.
    /// </summary>
    [OneTimeSetUp]
    public async Task SetUpAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        var dataInitialization = new VersionSeasonSix.DataInitialization(contextProvider, new NullLoggerFactory());
        await dataInitialization.CreateInitialDataAsync(1, true).ConfigureAwait(false);

        var context = contextProvider.CreateNewContext();
        this._gameConfiguration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();

        // The Guardiana de Icarus is added by its own update, which runs before on the server.
        await new AddIcarusGuardianPlugIn().ApplyUpdateAsync(context, this._gameConfiguration).ConfigureAwait(false);

        // Like on the server, the Ribbon Boxes (12/32-34) have the group 14, like the Chocolate Boxes (14/32-34).
        foreach (var ribbon in this._gameConfiguration.Items.Where(i => i.Group == 12 && i.Number is >= 32 and <= 34))
        {
            ribbon.Group = 14;
        }

        // Like the group "Box - Todas" of the server: boxes on all the maps, added in the admin panel.
        this._oldBoxes = context.CreateNew<DropItemGroup>();
        this._oldBoxes.Description = "Box - Todas";
        this._oldBoxes.Chance = 0.001;
        this._oldBoxes.PossibleItems.Add(this._gameConfiguration.Items.First(i => i.Name.ValueInNeutralLanguage == "Blue Chocolate Box"));
        this._gameConfiguration.DropItemGroups.Add(this._oldBoxes);
        foreach (var map in this._gameConfiguration.Maps.Where(m => m.DropItemGroups.Any(g => g.GetId() == JewelsGroupId)))
        {
            map.DropItemGroups.Add(this._oldBoxes);
        }

        // Like on the server: the Founder bonus is already configured for the beta.
        var founders = this._gameConfiguration.PlugInConfigurations.FirstOrDefault(c => c.TypeId == typeof(FounderExperienceBonusPlugIn).GUID);
        if (founders is null)
        {
            founders = context.CreateNew<PlugInConfiguration>();
            founders.TypeId = typeof(FounderExperienceBonusPlugIn).GUID;
            this._gameConfiguration.PlugInConfigurations.Add(founders);
        }

        founders.IsActive = true;
        founders.SetConfiguration(
            new FounderBonusConfiguration { CutoffDate = new DateTime(2026, 10, 12, 13, 0, 0, DateTimeKind.Utc), BonusMultiplier = 1.05f, TitleId = "fundador", RequireActiveTitle = false },
            null);

        await new ApplyEconomyForTheWipePlugIn().ApplyUpdateAsync(context, this._gameConfiguration).ConfigureAwait(false);
        await new ApplyEconomyForTheWipePlugIn().ApplyUpdateAsync(context, this._gameConfiguration).ConfigureAwait(false);
    }

    /// <summary>
    /// Tests the experience rates.
    /// </summary>
    [Test]
    public void ExperienceRatesAreLower()
    {
        Assert.That(this._gameConfiguration.ExperienceRate, Is.EqualTo(20));
        Assert.That(this._gameConfiguration.MasterExperienceRate, Is.EqualTo(20));
    }

    /// <summary>
    /// Tests that the resets cost zen and, from reset 6, packs of ten Bless.
    /// </summary>
    [Test]
    public void ResetsCostPacksOfBless()
    {
        var json = this.CustomConfiguration(typeof(ResetFeaturePlugIn).GUID);
        Assert.That((int)json["RequiredMoney"]!, Is.EqualTo(20_000_000));
        Assert.That((int)json["RequiredResetItemLevel"]!, Is.EqualTo(0));
        var packOfBless = this._gameConfiguration.Items.Single(i => i.Group == 12 && i.Number == 30);
        Assert.That((string)json["RequiredResetItem"]!["$ref"]!, Is.EqualTo(packOfBless.GetId().ToString()));
        var tiers = Values(json["ItemCostTiers"]!).Select(t => ((int)t!["MinimumResetCount"]!, (int)t["RequiredItemAmount"]!));
        Assert.That(tiers, Is.EqualTo(new[] { (6, 1), (11, 2), (16, 3) }));
    }

    /// <summary>
    /// Tests that the Founders are the accounts registered before the official launch, and only get the bonus with the title,
    /// while the bonus and the title stay as they were.
    /// </summary>
    [Test]
    public void FoundersAreRegisteredBeforeTheLaunch()
    {
        var plugInConfiguration = this.PlugInConfiguration(typeof(FounderExperienceBonusPlugIn).GUID);
        var configuration = plugInConfiguration.GetConfiguration<FounderBonusConfiguration>(null)!;
        Assert.That(configuration.CutoffDate, Is.EqualTo(new DateTime(2026, 12, 8, 3, 0, 0, DateTimeKind.Utc)));
        Assert.That(configuration.CutoffDate.Kind, Is.EqualTo(DateTimeKind.Utc));
        Assert.That(configuration.RequireActiveTitle, Is.True);
        Assert.That(configuration.BonusMultiplier, Is.EqualTo(1.05f));
        Assert.That(configuration.TitleId, Is.EqualTo("fundador"));
        Assert.That(plugInConfiguration.IsActive, Is.True);
    }

    /// <summary>
    /// Tests the configuration of the economy plugin.
    /// </summary>
    [Test]
    public void EconomyIsConfigured()
    {
        var configuration = this.PlugInConfiguration(typeof(EconomyFeaturePlugIn).GUID).GetConfiguration<EconomyConfiguration>(null)!;
        Assert.That(configuration.MoneyDropRate, Is.EqualTo(0.15f));
        Assert.That(configuration.AfkMoneyDropRate, Is.EqualTo(1));
        Assert.That(configuration.AfkValuableDropRate, Is.EqualTo(1));
    }

    /// <summary>
    /// Tests that the common jewels group only has the five jewels, and the other items have their own group on the same maps.
    /// </summary>
    [Test]
    public void JewelsGroupOnlyHasJewels()
    {
        var jewels = this._gameConfiguration.DropItemGroups.Single(g => g.GetId() == JewelsGroupId);
        Assert.That(jewels.Chance, Is.EqualTo(0.0015));
        Assert.That(jewels.PossibleItems.Select(i => i.Name.ValueInNeutralLanguage), Is.EquivalentTo(new[] { "Jewel of Chaos", "Jewel of Bless", "Jewel of Soul", "Jewel of Life", "Jewel of Creation" }));

        var misc = this._gameConfiguration.DropItemGroups.Single(g => g.GetId() == MiscGroupId);
        Assert.That(misc.PossibleItems, Has.Count.EqualTo(5));
        var mapsWithJewels = this._gameConfiguration.Maps.Where(m => m.DropItemGroups.Contains(jewels)).ToList();
        Assert.That(mapsWithJewels, Is.Not.Empty);
        Assert.That(mapsWithJewels.All(m => m.DropItemGroups.Count(g => g == misc) == 1), Is.True);
    }

    /// <summary>
    /// Tests that the high maps have their own jewel, once.
    /// </summary>
    /// <param name="mapNumber">The number of the map.</param>
    /// <param name="jewelName">The name of the jewel of the map.</param>
    [TestCase((short)33, "Jewel of Soul")]
    [TestCase((short)57, "Jewel of Bless")]
    [TestCase((short)63, "Jewel of Life")]
    public void HighMapsHaveTheirJewel(short mapNumber, string jewelName)
    {
        var map = this._gameConfiguration.Maps.First(m => m.Number == mapNumber);
        var mapJewelIds = Enumerable.Range(10, 7).Select(n => CreateUpdateGroupId((short)n)).ToHashSet();
        var groups = map.DropItemGroups.Where(g => mapJewelIds.Contains(g.GetId())).ToList();
        Assert.That(groups, Has.Count.EqualTo(1));
        Assert.That(groups[0].PossibleItems.Select(i => i.Name.ValueInNeutralLanguage), Does.Contain(jewelName));
    }

    /// <summary>
    /// Tests that the boxes drop by level of the monster, on the maps with jewels, and only once per map.
    /// </summary>
    [Test]
    public void BoxesDropByMonsterLevel()
    {
        var boxes = Enumerable.Range(30, 3).Select(n => this._gameConfiguration.DropItemGroups.Single(g => g.GetId() == CreateUpdateGroupId((short)n))).ToList();
        Assert.That(boxes.Select(b => (b.MinimumMonsterLevel, b.MaximumMonsterLevel)), Is.EqualTo(new (byte?, byte?)[] { (61, 89), (90, 109), (110, null) }));
        Assert.That(boxes.Select(b => b.Chance), Is.EqualTo(new[] { 0.0005, 0.0004, 0.0003 }));
        Assert.That(boxes.All(b => b.ItemLevel == 3), Is.True);
        Assert.That(boxes[0].PossibleItems.Select(i => i.Name.ValueInNeutralLanguage), Is.EquivalentTo(new[] { "Box of Luck", "Red Ribbon Box", "Pink Chocolate Box", "Cherry Blossom Play-Box" }));
        Assert.That(boxes[2].PossibleItems.Select(i => i.Name.ValueInNeutralLanguage), Is.EquivalentTo(new[] { "Blue Ribbon Box", "Blue Chocolate Box" }));

        var aida = this._gameConfiguration.Maps.First(m => m.Number == 33);
        Assert.That(boxes.All(b => aida.DropItemGroups.Count(g => g == b) == 1), Is.True);
        Assert.That(this._gameConfiguration.Maps.Any(m => m.DropItemGroups.Contains(this._oldBoxes)), Is.False);
    }

    /// <summary>
    /// Tests the chance of the excellent items.
    /// </summary>
    [Test]
    public void ExcellentItemsCanDrop()
    {
        var excellent = this._gameConfiguration.DropItemGroups.Single(g => g.GetId() == ExcellentGroupId);
        Assert.That(excellent.Chance, Is.EqualTo(0.00005));
    }

    /// <summary>
    /// Tests that the Guardiana de Icarus needs 10 resets and got twice the health only once.
    /// </summary>
    [Test]
    public void IcarusGuardianIsHarder()
    {
        var guardian = this._gameConfiguration.Monsters.Single(m => m.Number == IcarusGuardianNumber);
        Assert.That(guardian.Attributes.Single(a => a.AttributeDefinition?.Id == Stats.Resets.Id).Value, Is.EqualTo(10));
        Assert.That(guardian.Attributes.Single(a => a.AttributeDefinition?.Id == Stats.MaximumHealth.Id).Value, Is.EqualTo(230_000));
    }

    /// <summary>
    /// Tests that the golden invasion starts every 3 hours.
    /// </summary>
    [Test]
    public void GoldenInvasionEveryThreeHours()
    {
        var json = this.CustomConfiguration(typeof(GoldenInvasionPlugIn).GUID);
        Assert.That(Values(json["Timetable"]!).Select(t => (string)t!), Is.EqualTo(new[] { "00:00:00", "03:00:00", "06:00:00", "09:00:00", "12:00:00", "15:00:00", "18:00:00", "21:00:00" }));
    }

    /// <summary>
    /// Tests that the cash shop has tiers of +9 items per class and keeps its other settings.
    /// </summary>
    [Test]
    public void CashShopHasTiers()
    {
        var json = this.CustomConfiguration(typeof(CashShopPlugIn).GUID);
        Assert.That((string)json["CurrencyName"]!, Is.EqualTo("Cyber Coins"));
        var products = Values(json["Products"]!).Select(p => p!.AsObject()).ToList();
        var dk = products.Where(p => (string)p["Tab"]! == "DK").ToList();
        var weapons = dk.Where(p => (string)p["Category"]! == "Armas").Select(p => (int)p["Price"]!).ToList();
        Assert.That(weapons, Is.EqualTo(new[] { 200, 400, 800, 2000 }));
        Assert.That(dk.Count(p => (string)p["Category"]! == "Sets"), Is.EqualTo(20));
        Assert.That(products.Where(p => p["Item"] is not null && (string)p["Tab"]! != "Extras").All(p => (int)p["ItemLevel"]! <= 9), Is.True);
    }

    /// <summary>
    /// Creates the id of a drop group which the update adds, like <c>GuidHelper.CreateGuid&lt;DropItemGroup&gt;(31000, number)</c>.
    /// </summary>
    private static Guid CreateUpdateGroupId(short number) => new(0x200, 31000, number, 0, 0, 0, 0, 0, 0, 0, 0);

    private static IEnumerable<JsonNode?> Values(JsonNode node) => node as JsonArray ?? node["$values"]!.AsArray();

    private PlugInConfiguration PlugInConfiguration(Guid typeId) =>
        this._gameConfiguration.PlugInConfigurations.Single(c => c.TypeId == typeId);

    private JsonObject CustomConfiguration(Guid typeId) =>
        JsonNode.Parse(this.PlugInConfiguration(typeId).CustomConfiguration!)!.AsObject();
}
