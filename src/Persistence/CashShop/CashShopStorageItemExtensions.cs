// <copyright file="CashShopStorageItemExtensions.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.CashShop;

/// <summary>
/// Extensions to copy a <see cref="CashShopStorageItem"/>.
/// </summary>
public static class CashShopStorageItemExtensions
{
    /// <summary>
    /// Creates a shallow copy.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>The copy.</returns>
    public static CashShopStorageItem MemberwiseCloneItem(this CashShopStorageItem item) => new()
    {
        Id = item.Id,
        AccountId = item.AccountId,
        PackageSeq = item.PackageSeq,
        PriceSeq = item.PriceSeq,
        ProductSeq = item.ProductSeq,
        Price = item.Price,
        Name = item.Name,
        ItemGroup = item.ItemGroup,
        ItemNumber = item.ItemNumber,
        ItemLevel = item.ItemLevel,
        Quantity = item.Quantity,
        Skill = item.Skill,
        Luck = item.Luck,
        OptionLevel = item.OptionLevel,
        ExcellentOptions = item.ExcellentOptions,
        IsSkin = item.IsSkin,
        PurchasedAt = item.PurchasedAt,
        ConsumedAt = item.ConsumedAt,
        CharacterId = item.CharacterId,
    };
}
