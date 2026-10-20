// <copyright file="MerchantStorePrices.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.NPC;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;

/// <summary>
/// The prices which the merchant stores ask for their items, when an item of a store has a price
/// of its own (<see cref="Item.StorePrice"/>). Without one, the price is calculated as usual
/// (see <see cref="ItemPriceCalculator"/>).
/// </summary>
/// <remarks>
/// A price applies to an item definition of a level, in every merchant: its stores sell it for it,
/// and buy it back for a third of it. For items which stack, like potions, it's the price of one
/// piece, so a stack costs it times its pieces, like the calculated prices. The client gets the same prices (see the MerchantPrices
/// message), so it shows what the server asks. When several stores set a price for the same
/// item and level, the one of the opened merchant counts, and else the first one.
/// </remarks>
public static class MerchantStorePrices
{
    /// <summary>
    /// Gets the prices of the merchant stores of the configuration.
    /// </summary>
    /// <param name="configuration">The game configuration.</param>
    /// <param name="openedMerchant">The merchant which the player has opened, whose prices count first.</param>
    /// <returns>The buying price by item definition and level.</returns>
    public static IReadOnlyDictionary<(ItemDefinition Definition, byte Level), StorePrice> GetPrices(GameConfiguration configuration, MonsterDefinition? openedMerchant)
    {
        var prices = new Dictionary<(ItemDefinition Definition, byte Level), StorePrice>();
        var merchants = configuration.Monsters
            .Where(m => m.MerchantStore is not null)
            .OrderBy(m => m == openedMerchant ? 0 : 1)
            .ThenBy(m => m.Number);
        foreach (var merchant in merchants)
        {
            foreach (var item in merchant.MerchantStore!.Items.OrderBy(i => i.ItemSlot))
            {
                if (item is { Definition: { } definition, StorePrice: > 0 and var price })
                {
                    prices.TryAdd((definition, item.Level), new StorePrice(price, item.IsStackable()));
                }
            }
        }

        return prices;
    }

    /// <summary>
    /// Gets the buying price which the merchant stores ask for the item, if one of them sets one.
    /// </summary>
    /// <param name="configuration">The game configuration.</param>
    /// <param name="openedMerchant">The merchant which the player has opened, whose prices count first.</param>
    /// <param name="item">The item.</param>
    /// <returns>The buying price, or <c>null</c> when it's calculated as usual.</returns>
    public static long? FindPrice(GameConfiguration configuration, MonsterDefinition? openedMerchant, Item item)
    {
        return item.Definition is { } definition
               && GetPrices(configuration, openedMerchant).TryGetValue((definition, item.Level), out var price)
            ? price.PriceOf(item)
            : null;
    }

    /// <summary>
    /// The price which merchant stores ask for an item definition of a level.
    /// </summary>
    /// <param name="Price">The price, of one piece when <paramref name="IsPerPiece"/>.</param>
    /// <param name="IsPerPiece">If set to <c>true</c>, the item stacks and the price is the one of a piece.</param>
    public readonly record struct StorePrice(long Price, bool IsPerPiece)
    {
        /// <summary>
        /// Gets the price of the item: of all of its pieces, if it stacks.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns>The price.</returns>
        public long PriceOf(Item item) => this.IsPerPiece ? this.Price * Math.Max(1, (int)item.Durability()) : this.Price;
    }
}
