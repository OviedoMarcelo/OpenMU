// <copyright file="CashShopExampleCatalog.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.CashShop;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic.PlugIns.Cosmetics;

/// <summary>
/// An example catalog to start with: per class the best weapon and armor set without sockets, and in "Extras"
/// packs of jewels, boxes and the skins. The products are edited in the admin panel afterwards.
/// </summary>
public static class CashShopExampleCatalog
{
    private const string ExtrasTab = "Extras";
    private const int MaximumSkins = 8;
    private const byte ExampleLevel = 13;
    private const byte ExampleExcellentChoices = 2;
    private const int WeaponPrice = 1000;
    private const int ArmorPiecePrice = 800;
    private const int SkinPrice = 500;
    private const byte FirstArmorGroup = 7;
    private const byte LastArmorGroup = 11;
    private const byte ArmorGroup = 8;
    private const byte LastWeaponGroup = 5;
    private const byte BowGroup = 4;

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
        (1001, "Joyas", "Pack de 10 Bless", 12, 30, 0, 100),
        (1002, "Joyas", "Pack de 10 Soul", 12, 31, 0, 150),
        (1003, "Joyas", "Pack de 10 Life", 12, 136, 0, 200),
        (1004, "Joyas", "Pack de 10 Creation", 12, 137, 0, 150),
        (1005, "Joyas", "Pack de 10 Chaos", 12, 141, 0, 50),
        (1101, "Cajas", "Caja de Kundun +3", 14, 11, 10, 150),
        (1102, "Cajas", "Caja de Kundun +5", 14, 11, 12, 300),
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

            var weapon = Best(items.Where(i => i.Group <= LastWeaponGroup && IsForClass(i) && !IsAmmunition(i)));
            if (weapon is not null)
            {
                configuration.Products.Add(CreateExcellentProduct(++number, tab, "Armas", weapon, WeaponPrice));
            }

            var armor = Best(items.Where(i => i.Group == ArmorGroup && IsForClass(i)));
            if (armor is null)
            {
                continue;
            }

            for (var group = FirstArmorGroup; group <= LastArmorGroup; group++)
            {
                var piece = items.FirstOrDefault(i => i.Group == group && i.Number == armor.Number && IsForClass(i));
                if (piece is not null)
                {
                    configuration.Products.Add(CreateExcellentProduct(++number, tab, "Set", piece, ArmorPiecePrice));
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

    private static CashShopProductDefinition CreateExcellentProduct(int number, string tab, string category, ItemDefinition item, int price)
    {
        return new CashShopProductDefinition
        {
            Number = number,
            Tab = tab,
            Category = category,
            Item = item,
            ItemLevel = Math.Min(ExampleLevel, item.MaximumItemLevel),
            Skill = item.Skill is not null,
            Luck = true,
            ExcellentOptionChoices = ExampleExcellentChoices,
            Price = price,
        };
    }

    private static ItemDefinition? Best(IEnumerable<ItemDefinition> items)
    {
        return items
            .Where(i => CashShopCatalog.GetExcellentOptions(i).Count >= ExampleExcellentChoices)
            .OrderByDescending(i => i.DropLevel)
            .ThenByDescending(i => i.Number)
            .FirstOrDefault();
    }

    private static bool IsAmmunition(ItemDefinition item) => item.Group == BowGroup && item.Number is 7 or 15;
}
