// <copyright file="ApplyEconomyForTheWipePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.MuHelper;
using MUnique.OpenMU.GameLogic.PlugIns.CashShop;
using MUnique.OpenMU.GameLogic.PlugIns.Cosmetics;
using MUnique.OpenMU.GameLogic.PlugIns.Economy;
using MUnique.OpenMU.GameLogic.PlugIns.InvasionEvents;
using MUnique.OpenMU.GameLogic.Resets;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Applies the economy of MU Cyber for the wipe of December 2026: a lower experience rate, resets which cost
/// packs of jewels, less zen per kill, jewels which drop more and differ per map, excellent
/// items which can drop, a harder Flame of Condor, more golden invasions and the catalog of the cash shop in tiers.
/// All values can be adjusted afterwards in the admin panel.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("A8C7AD52-93B2-4001-8685-EF36B4644464")]
public class ApplyEconomyForTheWipePlugIn : UpdatePlugInBase
{
    /// <summary>
    /// The plug-in name.
    /// </summary>
    internal const string PlugInName = "Apply the economy for the wipe";

    /// <summary>
    /// The plug-in description.
    /// </summary>
    internal const string PlugInDescription = "Experience x10; boxes by monster level (61-89, 90-109, 110+) instead of all boxes on all maps, and less often; resets cost 20M zen per reset plus packs of ten Bless from reset 6 (1 pack), 11 (2) and 16 (3); zen per kill x0.3 (the same for players with the MU Helper or offline leveling); jewels drop with 0.15% and each high map has its own jewel; excellent items drop with 0.005%; the Guardiana de Icarus needs 10 resets and has twice the health; a golden invasion every 3 hours; a more expensive MU Helper; the cash shop with four tiers of +9 items per class (replaces its products).";

    /// <summary>
    /// The experience rate (and master experience rate).
    /// </summary>
    internal const float ExperienceRate = 10;

    /// <summary>
    /// The chance of the common jewels group, which drops the five jewels.
    /// </summary>
    internal const double CommonJewelChance = 0.0015;

    /// <summary>
    /// The chance of the excellent items.
    /// </summary>
    internal const double ExcellentChance = 0.00005;

    /// <summary>
    /// The resets which the Guardiana de Icarus needs to drop the Flame of Condor.
    /// </summary>
    internal const int IcarusGuardianRequiredResets = 10;

    /// <summary>
    /// The parent number of the ids of the drop groups which this update adds.
    /// </summary>
    internal const short GuidBase = 31000;

    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };

    private static readonly (byte Group, short Number) JewelOfChaos = (12, 15);
    private static readonly (byte Group, short Number) JewelOfBless = (14, 13);
    private static readonly (byte Group, short Number) JewelOfSoul = (14, 14);
    private static readonly (byte Group, short Number) JewelOfLife = (14, 16);
    private static readonly (byte Group, short Number) JewelOfCreation = (14, 22);
    private static readonly (byte Group, short Number) PackOfBless = (12, 30);

    /// <summary>
    /// The items which were in the jewels group but aren't jewels: Ale, Town Portal Scroll, Guardian Angel, Imp and
    /// Horn of Uniria. They get their own group, so that they don't take the chance of the jewels.
    /// </summary>
    private static readonly (byte Group, short Number)[] MiscItems = [(14, 9), (14, 10), (13, 0), (13, 1), (13, 2)];

    /// <summary>
    /// The boxes which monsters drop, by level of the monster, which replace the groups with boxes on all the maps
    /// ("Box - Todas"): the weaker boxes from weaker monsters, the boxes with more jewels and excellent items only
    /// from the strongest monsters, and all less often than before (0.1 %).
    /// The Box of Luck drops as +3, the Heart of Love; the other boxes have no levels.
    /// </summary>
    private static readonly (string Name, (byte Group, short Number)[] Items, double Chance, byte MinimumMonsterLevel, byte? MaximumMonsterLevel)[] Boxes =
    [
        ("Boxes, monsters 61-89: Heart of Love, Red Ribbon, Pink Chocolate, Cherry Blossom (0.05 %)", [(14, 11), (12, 32), (14, 32), (14, 84)], 0.0005, 61, 89),
        ("Boxes, monsters 90-109: Heart of Love, Green Ribbon, Red Chocolate (0.04 %)", [(14, 11), (12, 33), (14, 33)], 0.0004, 90, 109),
        ("Boxes, monsters 110+: Blue Ribbon, Blue Chocolate (0.03 %)", [(12, 34), (14, 34)], 0.0003, 110, null),
    ];

    /// <summary>
    /// The items of the <see cref="Boxes"/>.
    /// </summary>
    private static readonly (byte Group, short Number)[] BoxItems = [(14, 11), (12, 32), (12, 33), (12, 34), (14, 32), (14, 33), (14, 34), (14, 84)];

    /// <summary>
    /// The jewel of each high map, with its additional chance and the numbers of the maps.
    /// </summary>
    private static readonly (string Name, (byte Group, short Number)[] Items, double Chance, short[] Maps)[] MapJewels =
    [
        ("Tarkan: Jewel of Bless (0.05 %)", [JewelOfBless], 0.0005, [8]),
        ("Aida: Jewel of Soul (0.08 %)", [JewelOfSoul], 0.0008, [33]),
        ("Kanturu: Jewel of Life (0.08 %)", [JewelOfLife], 0.0008, [37, 38]),
        ("Swamp of Calmness: Jewel of Creation (0.08 %)", [JewelOfCreation], 0.0008, [56]),
        ("Raklion: Jewel of Bless (0.08 %)", [JewelOfBless], 0.0008, [57]),
        ("Karutan: Jewel of Chaos (0.1 %)", [JewelOfChaos], 0.001, [80, 81]),
        ("Vulcanus (PvP): all jewels (0.3 %)", [JewelOfChaos, JewelOfBless, JewelOfSoul, JewelOfLife, JewelOfCreation], 0.003, [63]),
    ];

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => false;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 25, 12, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        gameConfiguration.ExperienceRate = ExperienceRate;
        gameConfiguration.MasterExperienceRate = ExperienceRate;

        ApplyResets(context, gameConfiguration);
        ApplyEconomy(context, gameConfiguration);
        ApplyMuHelper(context, gameConfiguration);
        ApplyDrops(context, gameConfiguration);
        ApplyIcarusGuardian(gameConfiguration);
        ApplyGoldenInvasion(gameConfiguration);
        ApplyCashShop(context, gameConfiguration);
        return ValueTask.CompletedTask;
    }

    private static ItemDefinition? FindItem(GameConfiguration gameConfiguration, (byte Group, short Number) item)
    {
        return gameConfiguration.Items.FirstOrDefault(i => i.Group == item.Group && i.Number == item.Number);
    }

    private static PlugInConfiguration GetOrCreatePlugInConfiguration(IContext context, GameConfiguration gameConfiguration, Guid plugInId)
    {
        var plugInConfiguration = gameConfiguration.PlugInConfigurations.FirstOrDefault(c => c.TypeId == plugInId);
        if (plugInConfiguration is null)
        {
            plugInConfiguration = context.CreateNew<PlugInConfiguration>();
            plugInConfiguration.SetGuid(plugInId);
            plugInConfiguration.TypeId = plugInId;
            plugInConfiguration.IsActive = true;
            gameConfiguration.PlugInConfigurations.Add(plugInConfiguration);
        }

        return plugInConfiguration;
    }

    private static void ApplyResets(IContext context, GameConfiguration gameConfiguration)
    {
        var configuration = new ResetConfiguration
        {
            RequiredLevel = 400,
            LevelAfterReset = 1,
            RequiredMoney = 20_000_000,
            MultiplyRequiredMoneyByResetCount = true,
            RequiredResetItem = FindItem(gameConfiguration, PackOfBless),
            RequiredResetItemLevel = 0,
            ItemCostTiers =
            [
                new() { MinimumResetCount = 6, RequiredItemAmount = 1 },
                new() { MinimumResetCount = 11, RequiredItemAmount = 2 },
                new() { MinimumResetCount = 16, RequiredItemAmount = 3 },
            ],
            ResetStats = true,
            PointsPerReset = 1000,
            MultiplyPointsByResetCount = true,
            ReplacePointsPerReset = true,
            MoveHome = true,
            LogOut = true,
        };

        var plugInConfiguration = GetOrCreatePlugInConfiguration(context, gameConfiguration, typeof(ResetFeaturePlugIn).GUID);
        plugInConfiguration.IsActive = true;
        plugInConfiguration.SetConfiguration(configuration, new ExistingObjectsReferenceHandler());
    }

    private static void ApplyEconomy(IContext context, GameConfiguration gameConfiguration)
    {
        var plugInConfiguration = GetOrCreatePlugInConfiguration(context, gameConfiguration, typeof(EconomyFeaturePlugIn).GUID);
        plugInConfiguration.IsActive = true;
        plugInConfiguration.SetConfiguration(
            new EconomyConfiguration { MoneyDropRate = 0.3f, AfkMoneyDropRate = 1, AfkValuableDropRate = 1 },
            new ExistingObjectsReferenceHandler());
    }

    private static void ApplyMuHelper(IContext context, GameConfiguration gameConfiguration)
    {
        var plugInConfiguration = GetOrCreatePlugInConfiguration(context, gameConfiguration, typeof(MuHelperFeaturePlugIn).GUID);
        if (string.IsNullOrWhiteSpace(plugInConfiguration.CustomConfiguration)
            || JsonNode.Parse(plugInConfiguration.CustomConfiguration) is not JsonObject configuration)
        {
            plugInConfiguration.SetConfiguration(new MuHelperConfiguration(), new ExistingObjectsReferenceHandler());
            configuration = JsonNode.Parse(plugInConfiguration.CustomConfiguration!)!.AsObject();
        }

        // Zen per level and pay interval (5 minutes), per stage: at level 400 between 0.5M and 2.4M zen per hour.
        SetList(configuration, nameof(MuHelperConfiguration.CostPerStage), [100, 200, 300, 400, 500]);
        plugInConfiguration.CustomConfiguration = configuration.ToJsonString(IndentedJson);
    }

    /// <summary>
    /// Sets a list of a configuration which is stored with preserved references (a list as "$values") or without.
    /// </summary>
    private static void SetList(JsonObject configuration, string name, IEnumerable<JsonNode?> values)
    {
        var array = new JsonArray(values.ToArray());
        configuration[name] = configuration[name] is JsonObject
            ? new JsonObject { ["$id"] = string.Empty, ["$values"] = array }
            : array;
    }

    private static void ApplyDrops(IContext context, GameConfiguration gameConfiguration)
    {
        var jewelGroupId = GuidHelper.CreateGuid<DropItemGroup>(4);
        var mapsWithJewels = new List<GameMapDefinition>();
        if (gameConfiguration.DropItemGroups.FirstOrDefault(g => g.GetId() == jewelGroupId) is { } jewelGroup)
        {
            jewelGroup.Chance = CommonJewelChance;
            jewelGroup.Description = "The jewels drop item group (0.15 % drop chance)";
            jewelGroup.PossibleItems.Clear();
            foreach (var jewel in new[] { JewelOfChaos, JewelOfBless, JewelOfSoul, JewelOfLife, JewelOfCreation }.Select(j => FindItem(gameConfiguration, j)).OfType<ItemDefinition>())
            {
                jewelGroup.PossibleItems.Add(jewel);
            }

            mapsWithJewels.AddRange(gameConfiguration.Maps.Where(m => m.DropItemGroups.Contains(jewelGroup)));
        }

        var miscGroup = GetOrCreateDropGroup(context, gameConfiguration, 1, "Ale, Town Portal and pets (0.1 % drop chance)", 0.001, MiscItems);
        foreach (var map in mapsWithJewels.Where(m => !m.DropItemGroups.Contains(miscGroup)))
        {
            map.DropItemGroups.Add(miscGroup);
        }

        for (var i = 0; i < MapJewels.Length; i++)
        {
            var (name, items, chance, mapNumbers) = MapJewels[i];
            var group = GetOrCreateDropGroup(context, gameConfiguration, (short)(10 + i), name, chance, items);
            foreach (var map in gameConfiguration.Maps.Where(m => mapNumbers.Contains(m.Number) && !m.DropItemGroups.Contains(group)))
            {
                map.DropItemGroups.Add(group);
            }
        }

        ApplyBoxes(context, gameConfiguration);

        var excellentGroupId = GuidHelper.CreateGuid<DropItemGroup>(3);
        if (gameConfiguration.DropItemGroups.FirstOrDefault(g => g.GetId() == excellentGroupId) is { } excellentGroup)
        {
            excellentGroup.Chance = ExcellentChance;
            excellentGroup.Description = "The common drop item group for random excellent items (0.005 % drop chance)";
        }

        // The Flame of Condor of the Barracks of Balgass drops half as often; the Guardiana de Icarus is its main source.
        foreach (var flame in gameConfiguration.Maps.Where(m => m.Number == 41).SelectMany(m => m.DropItemGroups)
                     .Where(g => g.PossibleItems.Any(i => i.Group == 13 && i.Number == 52)))
        {
            flame.Chance = 0.0005;
        }
    }

    private static void ApplyBoxes(IContext context, GameConfiguration gameConfiguration)
    {
        var boxIds = new HashSet<Guid>(Enumerable.Range(0, Boxes.Length).Select(i => GuidHelper.CreateGuid<DropItemGroup>(GuidBase, (short)(30 + i))));
        bool IsBox(ItemDefinition item) => BoxItems.Contains((item.Group, item.Number));

        // The maps lose the groups of boxes which were added before (e.g. in the admin panel), and get the boxes by level instead.
        var maps = gameConfiguration.Maps.Where(m => m.DropItemGroups.Any(g => !boxIds.Contains(g.GetId()) && g.PossibleItems.Any(IsBox))).ToList();
        foreach (var map in maps)
        {
            foreach (var oldGroup in map.DropItemGroups.Where(g => !boxIds.Contains(g.GetId()) && g.PossibleItems.Any(IsBox)).ToList())
            {
                map.DropItemGroups.Remove(oldGroup);
            }
        }

        if (maps.Count == 0)
        {
            // Without boxes before, they drop on the maps with the common jewels group.
            var jewelGroupId = GuidHelper.CreateGuid<DropItemGroup>(4);
            maps.AddRange(gameConfiguration.Maps.Where(m => m.DropItemGroups.Any(g => g.GetId() == jewelGroupId)));
        }

        for (var i = 0; i < Boxes.Length; i++)
        {
            var (name, items, chance, minimumLevel, maximumLevel) = Boxes[i];
            var group = GetOrCreateDropGroup(context, gameConfiguration, (short)(30 + i), name, chance, items);
            group.ItemLevel = 3;
            group.MinimumMonsterLevel = minimumLevel;
            group.MaximumMonsterLevel = maximumLevel;
            foreach (var map in maps.Where(m => !m.DropItemGroups.Contains(group)))
            {
                map.DropItemGroups.Add(group);
            }
        }
    }

    private static DropItemGroup GetOrCreateDropGroup(IContext context, GameConfiguration gameConfiguration, short number, string description, double chance, (byte Group, short Number)[] items)
    {
        var id = GuidHelper.CreateGuid<DropItemGroup>(GuidBase, number);
        var group = gameConfiguration.DropItemGroups.FirstOrDefault(g => g.GetId() == id);
        if (group is null)
        {
            group = context.CreateNew<DropItemGroup>();
            group.SetGuid(GuidBase, number);
            gameConfiguration.DropItemGroups.Add(group);
        }

        group.Description = description;
        group.Chance = chance;

        // As jewels, they drop from monsters of any level above their drop level.
        group.ItemType = SpecialItemType.Jewel;
        group.PossibleItems.Clear();
        foreach (var item in items.Select(i => FindItem(gameConfiguration, i)).OfType<ItemDefinition>())
        {
            group.PossibleItems.Add(item);
        }

        return group;
    }

    private static void ApplyIcarusGuardian(GameConfiguration gameConfiguration)
    {
        if (gameConfiguration.Monsters.FirstOrDefault(m => m.Number == CustomMonstersInitialization.IcarusGuardianNumber) is not { } guardian)
        {
            return;
        }

        if (guardian.Attributes.FirstOrDefault(a => a.AttributeDefinition?.Id == Stats.Resets.Id) is { } resets)
        {
            resets.Value = IcarusGuardianRequiredResets;
        }

        // Only once: the initial health is 115.000.
        if (guardian.Attributes.FirstOrDefault(a => a.AttributeDefinition?.Id == Stats.MaximumHealth.Id) is { Value: < 200_000 } health)
        {
            health.Value *= 2;
        }
    }

    private static void ApplyGoldenInvasion(GameConfiguration gameConfiguration)
    {
        var plugInConfiguration = gameConfiguration.PlugInConfigurations.FirstOrDefault(c => c.TypeId == typeof(GoldenInvasionPlugIn).GUID);
        if (string.IsNullOrWhiteSpace(plugInConfiguration?.CustomConfiguration)
            || JsonNode.Parse(plugInConfiguration.CustomConfiguration) is not JsonObject configuration)
        {
            return;
        }

        // Every 3 hours instead of every 4 hours.
        SetList(configuration, "Timetable", Enumerable.Range(0, 8).Select(i => (JsonNode?)JsonValue.Create($"{i * 3:00}:00:00")));
        plugInConfiguration.CustomConfiguration = configuration.ToJsonString(IndentedJson);
    }

    private static void ApplyCashShop(IContext context, GameConfiguration gameConfiguration)
    {
        var plugInConfiguration = gameConfiguration.PlugInConfigurations.FirstOrDefault(c => c.TypeId == typeof(CashShopPlugIn).GUID);
        if (plugInConfiguration is null)
        {
            plugInConfiguration = GetOrCreatePlugInConfiguration(context, gameConfiguration, typeof(CashShopPlugIn).GUID);
            plugInConfiguration.IsActive = false;
        }

        var cosmetics = AddCashShopExampleCatalogPlugIn.ReadCosmetics(gameConfiguration.PlugInConfigurations.FirstOrDefault(c => c.TypeId == typeof(CosmeticsPlugIn).GUID));
        var catalog = CashShopExampleCatalog.Create(gameConfiguration, cosmetics);
        var catalogConfiguration = new PlugInConfiguration();
        catalogConfiguration.SetConfiguration(catalog, new ExistingObjectsReferenceHandler());

        // Only the products are replaced; the other settings (e.g. the name of the currency) stay.
        if (!string.IsNullOrWhiteSpace(plugInConfiguration.CustomConfiguration)
            && JsonNode.Parse(plugInConfiguration.CustomConfiguration) is JsonObject existing
            && JsonNode.Parse(catalogConfiguration.CustomConfiguration!) is JsonObject replacement)
        {
            existing[nameof(CashShopConfiguration.Products)] = replacement[nameof(CashShopConfiguration.Products)]?.DeepClone();
            plugInConfiguration.CustomConfiguration = existing.ToJsonString(IndentedJson);
        }
        else
        {
            plugInConfiguration.CustomConfiguration = catalogConfiguration.CustomConfiguration;
        }
    }
}
