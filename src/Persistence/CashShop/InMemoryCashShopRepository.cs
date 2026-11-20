// <copyright file="InMemoryCashShopRepository.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.CashShop;

using System.Threading;

/// <summary>
/// A <see cref="ICashShopRepository"/> which keeps everything only in memory.
/// It's used when no database backed repository is registered, e.g. for the demo mode and tests.
/// </summary>
public class InMemoryCashShopRepository : ICashShopRepository
{
    private readonly object _lock = new();
    private readonly Dictionary<Guid, long> _points = new();
    private readonly List<CashShopTransaction> _transactions = new();
    private readonly List<CashShopStorageItem> _items = new();
    private int _nextItemId = 1;

    /// <summary>
    /// Gets a copy of the recorded transactions, for tests.
    /// </summary>
    public IReadOnlyList<CashShopTransaction> Transactions
    {
        get
        {
            lock (this._lock)
            {
                return this._transactions.ToList();
            }
        }
    }

    /// <inheritdoc />
    public ValueTask<long> GetPointsAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        lock (this._lock)
        {
            return ValueTask.FromResult(this._points.GetValueOrDefault(accountId));
        }
    }

    /// <inheritdoc />
    public ValueTask<long?> AddPointsAsync(Guid accountId, long amount, string reason, string? reference = null, string? comment = null, CancellationToken cancellationToken = default)
    {
        lock (this._lock)
        {
            if (reference is not null && this._transactions.Any(t => t.AccountId == accountId && t.Reference == reference))
            {
                return ValueTask.FromResult<long?>(null);
            }

            var points = this._points.GetValueOrDefault(accountId) + amount;
            if (points < 0)
            {
                return ValueTask.FromResult<long?>(null);
            }

            this._points[accountId] = points;
            this.Record(accountId, amount, points, reason, reference, comment);
            return ValueTask.FromResult<long?>(points);
        }
    }

    /// <inheritdoc />
    /// <remarks>Without a database it doesn't know all accounts, so it reaches the accounts which had points already.</remarks>
    public ValueTask<int> AddPointsToAllAccountsAsync(long amount, string reason, string reference, string? comment = null, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        lock (this._lock)
        {
            var count = 0;
            foreach (var accountId in this._points.Keys.ToList())
            {
                if (this._transactions.Any(t => t.AccountId == accountId && t.Reference == reference))
                {
                    continue;
                }

                var points = this._points[accountId] + amount;
                this._points[accountId] = points;
                this.Record(accountId, amount, points, reason, reference, comment);
                count++;
            }

            return ValueTask.FromResult(count);
        }
    }

    /// <inheritdoc />
    public ValueTask<CashShopPurchaseResult> PurchaseAsync(CashShopStorageItem item, int maximumStorageItems, CancellationToken cancellationToken = default)
    {
        lock (this._lock)
        {
            if (this._items.Count(i => i.AccountId == item.AccountId && i.ConsumedAt is null) >= maximumStorageItems)
            {
                return ValueTask.FromResult(CashShopPurchaseResult.StorageFull);
            }

            var points = this._points.GetValueOrDefault(item.AccountId) - item.Price;
            if (points < 0)
            {
                return ValueTask.FromResult(CashShopPurchaseResult.NotEnoughPoints);
            }

            this._points[item.AccountId] = points;
            item.Id = this._nextItemId++;
            this._items.Add(Clone(item));
            this.Record(item.AccountId, -item.Price, points, "purchase", null, item.Name);
            return ValueTask.FromResult(CashShopPurchaseResult.Success);
        }
    }

    /// <inheritdoc />
    public ValueTask<(IList<CashShopStorageItem> Items, int TotalCount)> LoadStorageAsync(Guid accountId, int skip, int take, CancellationToken cancellationToken = default)
    {
        lock (this._lock)
        {
            var waiting = this._items.Where(i => i.AccountId == accountId && i.ConsumedAt is null).OrderBy(i => i.Id).ToList();
            IList<CashShopStorageItem> page = waiting.Skip(skip).Take(take).Select(Clone).ToList();
            return ValueTask.FromResult((page, waiting.Count));
        }
    }

    /// <inheritdoc />
    public ValueTask<CashShopStorageItem?> GetStorageItemAsync(Guid accountId, int id, CancellationToken cancellationToken = default)
    {
        lock (this._lock)
        {
            var item = this._items.FirstOrDefault(i => i.Id == id && i.AccountId == accountId && i.ConsumedAt is null);
            return ValueTask.FromResult(item is null ? null : Clone(item));
        }
    }

    /// <inheritdoc />
    public ValueTask<bool> MarkConsumedAsync(Guid accountId, int id, Guid characterId, CancellationToken cancellationToken = default)
    {
        lock (this._lock)
        {
            var item = this._items.FirstOrDefault(i => i.Id == id && i.AccountId == accountId && i.ConsumedAt is null);
            if (item is null)
            {
                return ValueTask.FromResult(false);
            }

            item.ConsumedAt = DateTime.UtcNow;
            item.CharacterId = characterId;
            return ValueTask.FromResult(true);
        }
    }

    /// <inheritdoc />
    public ValueTask UnmarkConsumedAsync(Guid accountId, int id, CancellationToken cancellationToken = default)
    {
        lock (this._lock)
        {
            if (this._items.FirstOrDefault(i => i.Id == id && i.AccountId == accountId) is { } item)
            {
                item.ConsumedAt = null;
                item.CharacterId = null;
            }

            return ValueTask.CompletedTask;
        }
    }

    private static CashShopStorageItem Clone(CashShopStorageItem item) => item.MemberwiseCloneItem();

    private void Record(Guid accountId, long amount, long pointsAfter, string reason, string? reference, string? comment)
    {
        this._transactions.Add(new CashShopTransaction
        {
            Id = this._transactions.Count + 1,
            AccountId = accountId,
            Amount = amount,
            PointsAfter = pointsAfter,
            Reason = reason,
            Reference = reference,
            Comment = comment,
            CreatedAt = DateTime.UtcNow,
        });
    }
}
