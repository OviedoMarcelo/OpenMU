// <copyright file="CashShopProductType.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.CashShop;

/// <summary>
/// What the player gets for a product of the cash shop.
/// </summary>
public enum CashShopProductType
{
    /// <summary>
    /// The item, which goes into the inventory.
    /// </summary>
    [Display(Name = "Ítem")]
    Item,

    /// <summary>
    /// The skin of the item (the cosmetic pieces of the plugin "Cosmetics"), which the character unlocks.
    /// For an armor piece it's the whole set, for wings and pets the skin itself.
    /// </summary>
    [Display(Name = "Skin")]
    Skin,
}
