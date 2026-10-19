// <copyright file="StorePrices.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AdminApi.Configuration;

using System.Globalization;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Web.AdminPanel.AdminApi.Audit;

/// <summary>
/// The items which the merchant NPCs sell, with their prices: the normal one, which is calculated
/// from the item, and the own price of the store (<see cref="Item.StorePrice"/>), if it has one.
/// </summary>
public static class StorePrices
{
    /// <summary>
    /// Gets the items which are sold by any merchant, one per item definition and level, with the
    /// stores which sell it.
    /// </summary>
    /// <param name="configuration">The game configuration.</param>
    /// <returns>The items on sale, ordered by their group, number and level.</returns>
    public static IReadOnlyList<ItemOnSale> GetItemsOnSale(GameConfiguration configuration)
    {
        var calculator = new ItemPriceCalculator();
        return configuration.Monsters
            .Where(monster => monster.MerchantStore is not null)
            .SelectMany(monster => monster.MerchantStore!.Items
                .Where(item => item.Definition is not null)
                .Select(item => (Monster: monster, Item: item)))
            .GroupBy(entry => (entry.Item.Definition!, entry.Item.Level))
            .Select(group => new ItemOnSale(
                group.Key.Item1.GetId(),
                group.Key.Item1.Name.ValueInNeutralLanguage,
                group.Key.Item1.Group,
                group.Key.Item1.Number,
                group.Key.Level,
                group
                    .OrderBy(entry => entry.Monster.Number)
                    .ThenBy(entry => entry.Item.ItemSlot)
                    .Select(entry => new StoreOffer(
                        entry.Item.GetId(),
                        entry.Monster.GetId(),
                        entry.Monster.Designation.ValueInNeutralLanguage,
                        entry.Monster.Number,
                        entry.Item.ItemSlot,
                        calculator.CalculateFinalBuyingPrice(entry.Item),
                        entry.Item.StorePrice))
                    .ToList()))
            .OrderBy(item => item.Group)
            .ThenBy(item => item.Number)
            .ThenBy(item => item.Level)
            .ToList();
    }

    /// <summary>
    /// Sets the own prices of store items. Nothing is changed when a change is invalid.
    /// </summary>
    /// <param name="configuration">The game configuration.</param>
    /// <param name="changes">The changes.</param>
    /// <returns>The changed merchants, with the changes for the audit log.</returns>
    /// <exception cref="ConfigurationValidationException">A change is invalid: an unknown item or a price which isn't positive.</exception>
    public static IReadOnlyList<MerchantPriceChanges> Apply(GameConfiguration configuration, IReadOnlyList<PriceChange> changes)
    {
        var storeItems = configuration.Monsters
            .Where(monster => monster.MerchantStore is not null)
            .SelectMany(monster => monster.MerchantStore!.Items.Select((item, index) => (Monster: monster, Item: item, Index: index)))
            .ToDictionary(entry => entry.Item.GetId());

        var errors = new List<FieldError>();
        for (var i = 0; i < changes.Count; i++)
        {
            if (!storeItems.ContainsKey(changes[i].ItemId))
            {
                errors.Add(new FieldError($"[{i}].ItemId", "El ítem ya no está en ninguna tienda; recargá la página."));
            }

            if (changes[i].Price is <= 0)
            {
                errors.Add(new FieldError($"[{i}].Price", "El precio tiene que ser mayor que cero."));
            }
        }

        if (errors.Count > 0)
        {
            throw new ConfigurationValidationException(errors);
        }

        var result = new Dictionary<MonsterDefinition, List<AuditChange>>();
        foreach (var change in changes)
        {
            var (monster, item, index) = storeItems[change.ItemId];
            if (item.StorePrice == change.Price)
            {
                continue;
            }

            var caption = $"Precio propio de {item.Definition?.Name.ValueInNeutralLanguage ?? "ítem"} +{item.Level} (casillero {item.ItemSlot})";
            var auditChange = new AuditChange($"MerchantStore.Items[{index}].StorePrice", caption, Describe(item.StorePrice), Describe(change.Price));
            item.StorePrice = change.Price;
            if (!result.TryGetValue(monster, out var monsterChanges))
            {
                result[monster] = monsterChanges = [];
            }

            monsterChanges.Add(auditChange);
        }

        return result.Select(entry => new MerchantPriceChanges(entry.Key, entry.Value)).ToList();
    }

    private static string? Describe(int? price) => price?.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// An item definition and level which is sold by at least one store.
    /// </summary>
    /// <param name="DefinitionId">The id of the item definition.</param>
    /// <param name="Name">The name of the item.</param>
    /// <param name="Group">The group of the item.</param>
    /// <param name="Number">The number of the item in its group.</param>
    /// <param name="Level">The level of the item.</param>
    /// <param name="Offers">The store items which sell it.</param>
    public record ItemOnSale(Guid DefinitionId, string Name, int Group, int Number, int Level, IReadOnlyList<StoreOffer> Offers);

    /// <summary>
    /// An item in the store of a merchant.
    /// </summary>
    /// <param name="ItemId">The id of the item in the store.</param>
    /// <param name="MerchantId">The id of the merchant (monster definition).</param>
    /// <param name="MerchantName">The name of the merchant.</param>
    /// <param name="MerchantNumber">The number of the merchant.</param>
    /// <param name="Slot">The slot of the item in the store.</param>
    /// <param name="NormalPrice">The price which is calculated from the item, when the store has no own price.</param>
    /// <param name="StorePrice">The own price of the store, if any.</param>
    public record StoreOffer(Guid ItemId, Guid MerchantId, string MerchantName, int MerchantNumber, int Slot, long NormalPrice, int? StorePrice);

    /// <summary>
    /// A change of the own price of a store item.
    /// </summary>
    /// <param name="ItemId">The id of the item in the store.</param>
    /// <param name="Price">The own price, or <c>null</c> to use the normal price.</param>
    public record PriceChange(Guid ItemId, int? Price);

    /// <summary>
    /// The changed prices of a merchant.
    /// </summary>
    /// <param name="Merchant">The merchant.</param>
    /// <param name="Changes">The changes, for the audit log.</param>
    public record MerchantPriceChanges(MonsterDefinition Merchant, IReadOnlyList<AuditChange> Changes);
}
