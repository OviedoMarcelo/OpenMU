// <copyright file="CashShopExampleCatalog.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.CashShop;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic.PlugIns.Cosmetics;

/// <summary>
/// The catalog of MU Cyber: per class four tiers of weapons and armor sets without sockets (from a basic one to
/// the best one, which is much more expensive), and in "Extras" packs of jewels, boxes and the skins.
/// The items are +9 with luck: higher levels are made with jewels, which can be farmed or bought.
/// The products are edited in the admin panel afterwards.
/// </summary>
public static class CashShopExampleCatalog
{
    private const string ExtrasTab = "Extras";
    private const int MaximumSkins = 8;
    private const byte ItemLevel = 9;
    private const int SkinPrice = 500;
    private const byte FirstArmorGroup = 7;
    private const byte LastArmorGroup = 11;
    private const byte ArmorGroup = 8;
    private const byte LastWeaponGroup = 5;
    private const byte BowGroup = 4;

    /// <summary>
    /// The tiers, from the weakest to the strongest: the price of a weapon, the price of an armor piece and how many
    /// excellent options the buyer chooses. The strongest tier is much more expensive than the others.
    /// </summary>
    private static readonly (int WeaponPrice, int ArmorPiecePrice, byte ExcellentChoices)[] Tiers =
    [
        (200, 150, 1),
        (400, 300, 1),
        (800, 600, 1),
        (2000, 1500, 2),
    ];

    /// <summary>
    /// The classes, in the order of their tabs: the short name of the tab and the first number of the class
    /// (each class has four numbers, e.g. 4 to 7 for the Dark Knight and its evolutions).
    /// </summary>
    private static readonly (string Tab, int FirstClassNumber)[] Classes =
    [
        ("DK", 4),
        ("DW", 0),
        ("Elfa", 8),
        ("MG", 12),
        ("DL", 16),
        ("SUM", 20),
        ("RF", 24),
    ];

    /// <summary>
    /// The extras which are items: number, category, name, item group and number, item level and price.
    /// The packed jewels have 10 jewels at level 0.
    /// </summary>
    private static readonly (int Number, string Category, string Name, byte Group, short ItemNumber, byte Level, int Price)[] ItemExtras =
    [
        (1001, "Joyas", "Pack de 10 Bless", 12, 30, 0, 300),
        (1002, "Joyas", "Pack de 10 Soul", 12, 31, 0, 400),
        (1003, "Joyas", "Pack de 10 Life", 12, 136, 0, 500),
        (1004, "Joyas", "Pack de 10 Creation", 12, 137, 0, 400),
        (1005, "Joyas", "Pack de 10 Chaos", 12, 141, 0, 150),
        (1101, "Cajas", "Caja de Kundun +3", 14, 11, 10, 300),
        (1102, "Cajas", "Caja de Kundun +5", 14, 11, 12, 600),
    ];

    /// <summary>
    /// Creates the example configuration.
    /// </summary>
    /// <param name="gameConfiguration">The game configuration with the items.</param>
    /// <param name="cosmetics">The configuration of the cosmetics plugin with the skins; <c>null</c> for none.</param>
    /// <returns>The configuration of the cash shop.</returns>
    public static CashShopConfiguration Create(GameConfiguration gameConfiguration, CosmeticsConfiguration? cosmetics)
    {
        var configuration = new CashShopConfiguration();
        var items = gameConfiguration.Items.Where(i => i.MaximumSockets == 0).ToList();
        for (var classIndex = 0; classIndex < Classes.Length; classIndex++)
        {
            var (tab, firstClassNumber) = Classes[classIndex];
            var number = (classIndex + 1) * 100;
            bool IsForClass(ItemDefinition item) => item.QualifiedCharacters.Any(c => c.Number >= firstClassNumber && c.Number < firstClassNumber + 4);

            var weapons = PickTiers(items.Where(i => i.Group <= LastWeaponGroup && IsForClass(i) && !IsAmmunition(i) && !IsForManyClasses(i)));
            foreach (var (weapon, tier) in weapons)
            {
                var (weaponPrice, _, choices) = Tiers[tier];
                configuration.Products.Add(CreateExcellentProduct(++number, tab, "Armas", weapon, weaponPrice, choices));
            }

            var armors = PickTiers(items.Where(i => i.Group == ArmorGroup && IsForClass(i)));
            foreach (var (armor, tier) in armors)
            {
                var (_, piecePrice, choices) = Tiers[tier];
                for (var group = FirstArmorGroup; group <= LastArmorGroup; group++)
                {
                    var piece = items.FirstOrDefault(i => i.Group == group && i.Number == armor.Number && IsForClass(i));
                    if (piece is not null)
                    {
                        configuration.Products.Add(CreateExcellentProduct(++number, tab, "Sets", piece, piecePrice, choices));
                    }
                }
            }
        }

        foreach (var extra in ItemExtras)
        {
            if (gameConfiguration.Items.FirstOrDefault(i => i.Group == extra.Group && i.Number == extra.ItemNumber) is { } item)
            {
                configuration.Products.Add(new CashShopProductDefinition
                {
                    Number = extra.Number,
                    Tab = ExtrasTab,
                    Category = extra.Category,
                    Name = extra.Name,
                    Item = item,
                    ItemLevel = extra.Level,
                    Price = extra.Price,
                });
            }
        }

        AddSkins(configuration, gameConfiguration, cosmetics);
        return configuration;
    }

    private static void AddSkins(CashShopConfiguration configuration, GameConfiguration gameConfiguration, CosmeticsConfiguration? cosmetics)
    {
        if (cosmetics is null)
        {
            return;
        }

        // An armor set is one skin, shown with its armor; wings and pets are a skin each.
        var skins = cosmetics.Pieces
            .Where(p => p.Number > 0)
            .Select(p => CosmeticSlotExtensions.ArmorSlots.Contains(p.Slot) ? (Group: ArmorGroup, p.ItemNumber) : (Group: p.Slot.GetItemGroup(), p.ItemNumber))
            .Distinct()
            .Select(key => gameConfiguration.Items.FirstOrDefault(i => i.Group == key.Group && i.Number == key.ItemNumber))
            .OfType<ItemDefinition>()
            .Take(MaximumSkins);
        var number = 1200;
        foreach (var item in skins)
        {
            configuration.Products.Add(new CashShopProductDefinition
            {
                Number = ++number,
                Tab = ExtrasTab,
                Category = "Skins",
                Type = CashShopProductType.Skin,
                Item = item,
                Description = item.Group == ArmorGroup
                    ? "Desbloquea el set entero como skin en el personaje con el que la canjees. Solo la usan las clases que pueden equiparse ese set."
                    : string.Empty,
                Price = SkinPrice,
            });
        }
    }

    private static CashShopProductDefinition CreateExcellentProduct(int number, string tab, string category, ItemDefinition item, int price, byte excellentChoices)
    {
        return new CashShopProductDefinition
        {
            Number = number,
            Tab = tab,
            Category = category,
            Item = item,
            ItemLevel = Math.Min(ItemLevel, item.MaximumItemLevel),
            Skill = item.Skill is not null,
            Luck = true,
            ExcellentOptionChoices = excellentChoices,
            Price = price,
        };
    }

    /// <summary>
    /// Picks one item per tier, from the weakest to the strongest, spread over the possible items ordered by their
    /// drop level: the first one is the weakest and the last one the strongest.
    /// </summary>
    /// <param name="items">The possible items.</param>
    /// <returns>
    /// The items with the index of their tier. When there are fewer possible items than tiers, they get the
    /// strongest tiers, so that the best item always has the price of the strongest tier.
    /// </returns>
    private static IReadOnlyList<(ItemDefinition Item, int Tier)> PickTiers(IEnumerable<ItemDefinition> items)
    {
        var maximumChoices = Tiers.Max(t => t.ExcellentChoices);
        var possible = items
            .Where(i => CashShopCatalog.GetExcellentOptions(i).Count >= maximumChoices)
            .OrderBy(i => i.DropLevel)
            .ThenBy(i => i.Number)
            .ToList();
        if (possible.Count <= Tiers.Length)
        {
            var firstTier = Tiers.Length - possible.Count;
            return possible.Select((item, index) => (item, firstTier + index)).ToList();
        }

        return Enumerable.Range(0, Tiers.Length)
            .Select(tier => (possible[(int)Math.Round(tier * (possible.Count - 1) / (double)(Tiers.Length - 1))], tier))
            .ToList();
    }

    /// <summary>
    /// Determines whether a weapon is for more than two classes, like the Small Axe. Such weapons aren't typical
    /// for a class, so they're not offered.
    /// </summary>
    /// <param name="item">The weapon.</param>
    /// <returns><c>true</c>, if more than two classes (with their evolutions) can use it.</returns>
    private static bool IsForManyClasses(ItemDefinition item) =>
        item.QualifiedCharacters.Select(c => c.Number / 4).Distinct().Count() > 2;

    private static bool IsAmmunition(ItemDefinition item) => item.Group == BowGroup && item.Number is 7 or 15;
}
