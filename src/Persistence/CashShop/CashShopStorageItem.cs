// <copyright file="CashShopStorageItem.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.CashShop;

/// <summary>
/// An item which an account bought in the cash shop. It waits in the storage of the cash shop
/// until the player uses it, which moves it into the inventory of the character.
/// </summary>
/// <remarks>
/// It keeps everything which describes the item, so that later changes of the catalog don't
/// change what the player bought.
/// </remarks>
public class CashShopStorageItem
{
    /// <summary>
    /// Gets or sets the id. The client knows the item by it.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the id of the account.
    /// </summary>
    public Guid AccountId { get; set; }

    /// <summary>
    /// Gets or sets the number of the package (product of the catalog) which was bought.
    /// </summary>
    public int PackageSeq { get; set; }

    /// <summary>
    /// Gets or sets the number of the price (variant of the package) which was bought.
    /// </summary>
    public int PriceSeq { get; set; }

    /// <summary>
    /// Gets or sets the number of the product of the catalog files.
    /// </summary>
    public int ProductSeq { get; set; }

    /// <summary>
    /// Gets or sets the paid points.
    /// </summary>
    public long Price { get; set; }

    /// <summary>
    /// Gets or sets the shown name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the group of the item definition.
    /// </summary>
    public byte ItemGroup { get; set; }

    /// <summary>
    /// Gets or sets the number of the item definition.
    /// </summary>
    public short ItemNumber { get; set; }

    /// <summary>
    /// Gets or sets the item level.
    /// </summary>
    public byte ItemLevel { get; set; }

    /// <summary>
    /// Gets or sets the amount of items, or the pieces of a stack.
    /// </summary>
    public byte Quantity { get; set; } = 1;

    /// <summary>
    /// Gets or sets a value indicating whether the item has its skill.
    /// </summary>
    public bool Skill { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the item has luck.
    /// </summary>
    public bool Luck { get; set; }

    /// <summary>
    /// Gets or sets the level of the additional option (0 to 4).
    /// </summary>
    public byte OptionLevel { get; set; }

    /// <summary>
    /// Gets or sets the excellent options, as a bit mask like the one of the /item command.
    /// </summary>
    public byte ExcellentOptions { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the player gets the skin of the item (its cosmetic pieces) instead of the item.
    /// </summary>
    public bool IsSkin { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the purchase.
    /// </summary>
    public DateTime PurchasedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the player used the item; <c>null</c> while it waits in the storage.
    /// </summary>
    public DateTime? ConsumedAt { get; set; }

    /// <summary>
    /// Gets or sets the character which got the item.
    /// </summary>
    public Guid? CharacterId { get; set; }
}
