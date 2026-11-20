// <copyright file="ICashShopViewPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Views.CashShop;

using MUnique.OpenMU.Persistence.CashShop;

/// <summary>
/// The result of buying in the cash shop, with the values which the client knows.
/// </summary>
public enum CashShopBuyResult : byte
{
    /// <summary>
    /// The item was bought and is in the storage.
    /// </summary>
    Success = 0,

    /// <summary>
    /// The player doesn't have enough points.
    /// </summary>
    NotEnoughPoints = 1,

    /// <summary>
    /// The storage is full.
    /// </summary>
    StorageFull = 2,

    /// <summary>
    /// The product isn't in the catalog (anymore).
    /// </summary>
    NotAvailable = 4,

    /// <summary>
    /// The product can't be bought.
    /// </summary>
    CannotBuy = 6,

    /// <summary>
    /// The server couldn't process it, e.g. because the database isn't available.
    /// </summary>
    Error = 0xFF,
}

/// <summary>
/// The result of using an item of the storage of the cash shop, with the values which the client knows.
/// </summary>
public enum CashShopConsumeResult : byte
{
    /// <summary>
    /// The item is in the inventory.
    /// </summary>
    Success = 0,

    /// <summary>
    /// The item isn't in the storage.
    /// </summary>
    NotFound = 1,

    /// <summary>
    /// The inventory doesn't have enough space.
    /// </summary>
    NoInventorySpace = 21,

    /// <summary>
    /// The item can't be used.
    /// </summary>
    CannotUse = 22,

    /// <summary>
    /// The server couldn't process it, e.g. because the database isn't available.
    /// </summary>
    Error = 0xFF,
}

/// <summary>
/// The view of the cash shop of a player.
/// </summary>
public interface ICashShopViewPlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows the points of the account.
    /// </summary>
    /// <param name="points">The points.</param>
    ValueTask ShowPointsAsync(long points);

    /// <summary>
    /// Shows whether the shop can be opened.
    /// </summary>
    /// <param name="success">If set, the client opens the shop.</param>
    ValueTask ShowOpenResultAsync(bool success);

    /// <summary>
    /// Shows the result of a purchase.
    /// </summary>
    /// <param name="result">The result.</param>
    ValueTask ShowBuyResultAsync(CashShopBuyResult result);

    /// <summary>
    /// Shows a page of the storage.
    /// </summary>
    /// <param name="items">The items of the page.</param>
    /// <param name="totalCount">The count of all items of the storage.</param>
    /// <param name="page">The page, starting at 1; 0 when the storage is empty.</param>
    /// <param name="totalPages">The count of pages.</param>
    ValueTask ShowStorageAsync(IList<CashShopStorageItem> items, int totalCount, int page, int totalPages);

    /// <summary>
    /// Shows the result of using an item of the storage.
    /// </summary>
    /// <param name="result">The result.</param>
    ValueTask ShowConsumeResultAsync(CashShopConsumeResult result);

    /// <summary>
    /// Tells the client the version of the catalog files, which it downloads when it doesn't have them yet.
    /// </summary>
    /// <param name="salesZone">The sales zone.</param>
    /// <param name="year">The year.</param>
    /// <param name="yearId">The year id.</param>
    ValueTask ShowCatalogVersionAsync(ushort salesZone, ushort year, ushort yearId);

    /// <summary>
    /// Shows the items of an event category. The shop has no events, so the list is always empty.
    /// </summary>
    ValueTask ShowEmptyEventItemListAsync();
}
