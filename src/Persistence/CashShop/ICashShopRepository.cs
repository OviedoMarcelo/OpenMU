// <copyright file="ICashShopRepository.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.CashShop;

using System.Threading;

/// <summary>
/// The result of a purchase in the cash shop.
/// </summary>
public enum CashShopPurchaseResult
{
    /// <summary>
    /// The points were taken and the item is in the storage.
    /// </summary>
    Success,

    /// <summary>
    /// The account doesn't have enough points.
    /// </summary>
    NotEnoughPoints,

    /// <summary>
    /// The storage of the account is full.
    /// </summary>
    StorageFull,
}

/// <summary>
/// Keeps the points of the accounts and the items which they bought in the cash shop.
/// Changes of the points are atomic: a purchase takes the points and stores the item at once.
/// </summary>
public interface ICashShopRepository
{
    /// <summary>
    /// Gets the points of an account.
    /// </summary>
    /// <param name="accountId">The account id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The points; 0 when the account never had any.</returns>
    ValueTask<long> GetPointsAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds (or with a negative amount, removes) points of an account.
    /// </summary>
    /// <param name="accountId">The account id.</param>
    /// <param name="amount">The amount. A negative amount never takes more points than the account has.</param>
    /// <param name="reason">The reason, e.g. "gm".</param>
    /// <param name="reference">An optional reference which can be used only once per account, e.g. "referral:&lt;id&gt;".</param>
    /// <param name="comment">An optional free text.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The points after the change; <c>null</c> when the reference was used already or a negative amount exceeds the points.</returns>
    ValueTask<long?> AddPointsAsync(Guid accountId, long amount, string reason, string? reference = null, string? comment = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds points to every account which didn't get the points of this reference yet, e.g. a gift for all
    /// players. Doing it again with the same reference only reaches the accounts which were created since then.
    /// </summary>
    /// <param name="amount">The amount; it has to be positive.</param>
    /// <param name="reason">The reason, e.g. "gift".</param>
    /// <param name="reference">The reference which every account can get only once, e.g. "gift:beta-2026-12".</param>
    /// <param name="comment">An optional free text.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The count of accounts which got the points now.</returns>
    ValueTask<int> AddPointsToAllAccountsAsync(long amount, string reason, string reference, string? comment = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes the price from the points of the account and puts the item into its storage.
    /// </summary>
    /// <param name="item">The item, with the account id and the price. Its <see cref="CashShopStorageItem.Id"/> is set on success.</param>
    /// <param name="maximumStorageItems">The maximum count of items which wait in the storage of an account.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    ValueTask<CashShopPurchaseResult> PurchaseAsync(CashShopStorageItem item, int maximumStorageItems, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a page of the items which wait in the storage of an account, the oldest first.
    /// </summary>
    /// <param name="accountId">The account id.</param>
    /// <param name="skip">The count of items to skip.</param>
    /// <param name="take">The count of items to load.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The items of the page and the count of all waiting items.</returns>
    ValueTask<(IList<CashShopStorageItem> Items, int TotalCount)> LoadStorageAsync(Guid accountId, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an item which waits in the storage of the account.
    /// </summary>
    /// <param name="accountId">The account id.</param>
    /// <param name="id">The id of the item.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The item; <c>null</c>, if it doesn't exist, belongs to another account or was used already.</returns>
    ValueTask<CashShopStorageItem?> GetStorageItemAsync(Guid accountId, int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks an item of the storage as used by a character, when it wasn't used yet.
    /// </summary>
    /// <param name="accountId">The account id.</param>
    /// <param name="id">The id of the item.</param>
    /// <param name="characterId">The character which gets the item.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><c>true</c>, if this call marked it; <c>false</c>, if it was used already or doesn't exist.</returns>
    ValueTask<bool> MarkConsumedAsync(Guid accountId, int id, Guid characterId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Puts a used item back into the storage, when it couldn't be given to the character after all.
    /// </summary>
    /// <param name="accountId">The account id.</param>
    /// <param name="id">The id of the item.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The task.</returns>
    ValueTask UnmarkConsumedAsync(Guid accountId, int id, CancellationToken cancellationToken = default);
}
