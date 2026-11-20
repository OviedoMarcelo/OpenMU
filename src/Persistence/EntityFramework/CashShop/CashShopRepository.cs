// <copyright file="CashShopRepository.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework.CashShop;

using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.Persistence.CashShop;
using Nito.AsyncEx;
using Npgsql;

/// <summary>
/// The database backed <see cref="ICashShopRepository"/>. It creates its own "cashshop" schema by migration.
/// </summary>
/// <remarks>
/// Every change of the points locks the row of the account, so parallel purchases of the same
/// account (e.g. from two game servers) can never spend the same points twice.
/// </remarks>
public sealed class CashShopRepository : ICashShopRepository, IDisposable
{
    private const string UniqueViolation = "23505";

    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(30);

    private readonly ILogger<CashShopRepository> _logger;
    private readonly SetupService? _setupService;
    private readonly AsyncLock _storageLock = new();
    private bool _isStorageReady;
    private DateTime _nextProbeAt = DateTime.MinValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="CashShopRepository"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="setupService">The setup service, which tells when the database got re-created.</param>
    public CashShopRepository(ILogger<CashShopRepository> logger, SetupService? setupService = null)
    {
        this._logger = logger;
        this._setupService = setupService;
        if (this._setupService is not null)
        {
            this._setupService.DatabaseInitialized += this.OnDatabaseInitializedAsync;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (this._setupService is not null)
        {
            this._setupService.DatabaseInitialized -= this.OnDatabaseInitializedAsync;
        }
    }

    /// <inheritdoc />
    public async ValueTask<long> GetPointsAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        await this.EnsureAvailableStorageAsync(cancellationToken).ConfigureAwait(false);

        await using var context = new CashShopContext();
        return await context.Balances
            .AsNoTracking()
            .Where(b => b.AccountId == accountId)
            .Select(b => b.Points)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<long?> AddPointsAsync(Guid accountId, long amount, string reason, string? reference = null, string? comment = null, CancellationToken cancellationToken = default)
    {
        await this.EnsureAvailableStorageAsync(cancellationToken).ConfigureAwait(false);

        await using var context = new CashShopContext();
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var points = await LockPointsAsync(context, accountId, cancellationToken).ConfigureAwait(false) + amount;
            if (points < 0)
            {
                return null;
            }

            await SetPointsAsync(context, accountId, points, cancellationToken).ConfigureAwait(false);
            context.Transactions.Add(CreateTransaction(accountId, amount, points, reason, reference, comment));
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return points;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: UniqueViolation })
        {
            // The reference was used already, e.g. the points of a referral were given before.
            return null;
        }
    }

    /// <inheritdoc />
    public async ValueTask<int> AddPointsToAllAccountsAsync(long amount, string reason, string reference, string? comment = null, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        await this.EnsureAvailableStorageAsync(cancellationToken).ConfigureAwait(false);

        reason = reason.Length > 32 ? reason[..32] : reason;
        comment = comment is { Length: > 256 } ? comment[..256] : comment;

        // One statement: the accounts without a transaction of the reference get the points (the balance row is
        // created when missing) and a transaction each. The upsert locks the balance rows like the other changes.
        await using var context = new CashShopContext();
        return await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            WITH targets AS (
                SELECT a."Id" FROM data."Account" a
                WHERE NOT EXISTS (
                    SELECT 1 FROM cashshop."Transaction" t WHERE t."AccountId" = a."Id" AND t."Reference" = {reference})
            ), balances AS (
                INSERT INTO cashshop."Balance" ("AccountId", "Points", "UpdatedAt")
                SELECT "Id", {amount}, now() FROM targets
                ON CONFLICT ("AccountId") DO UPDATE SET "Points" = cashshop."Balance"."Points" + EXCLUDED."Points", "UpdatedAt" = now()
                RETURNING "AccountId", "Points"
            )
            INSERT INTO cashshop."Transaction" ("AccountId", "Amount", "PointsAfter", "Reason", "Reference", "Comment", "CreatedAt")
            SELECT "AccountId", {amount}, "Points", {reason}, {reference}, {comment}, now() FROM balances
            """,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<CashShopPurchaseResult> PurchaseAsync(CashShopStorageItem item, int maximumStorageItems, CancellationToken cancellationToken = default)
    {
        await this.EnsureAvailableStorageAsync(cancellationToken).ConfigureAwait(false);

        await using var context = new CashShopContext();
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var points = await LockPointsAsync(context, item.AccountId, cancellationToken).ConfigureAwait(false) - item.Price;
        if (points < 0)
        {
            return CashShopPurchaseResult.NotEnoughPoints;
        }

        var waiting = await context.StorageItems
            .CountAsync(i => i.AccountId == item.AccountId && i.ConsumedAt == null, cancellationToken)
            .ConfigureAwait(false);
        if (waiting >= maximumStorageItems)
        {
            return CashShopPurchaseResult.StorageFull;
        }

        await SetPointsAsync(context, item.AccountId, points, cancellationToken).ConfigureAwait(false);
        var stored = item.MemberwiseCloneItem();
        stored.Id = 0;
        context.StorageItems.Add(stored);
        context.Transactions.Add(CreateTransaction(item.AccountId, -item.Price, points, "purchase", null, item.Name));
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        item.Id = stored.Id;
        return CashShopPurchaseResult.Success;
    }

    /// <inheritdoc />
    public async ValueTask<(IList<CashShopStorageItem> Items, int TotalCount)> LoadStorageAsync(Guid accountId, int skip, int take, CancellationToken cancellationToken = default)
    {
        await this.EnsureAvailableStorageAsync(cancellationToken).ConfigureAwait(false);

        await using var context = new CashShopContext();
        var waiting = context.StorageItems.AsNoTracking().Where(i => i.AccountId == accountId && i.ConsumedAt == null);
        var total = await waiting.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await waiting.OrderBy(i => i.Id).Skip(skip).Take(take).ToListAsync(cancellationToken).ConfigureAwait(false);
        return (items, total);
    }

    /// <inheritdoc />
    public async ValueTask<CashShopStorageItem?> GetStorageItemAsync(Guid accountId, int id, CancellationToken cancellationToken = default)
    {
        await this.EnsureAvailableStorageAsync(cancellationToken).ConfigureAwait(false);

        await using var context = new CashShopContext();
        return await context.StorageItems
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == id && i.AccountId == accountId && i.ConsumedAt == null, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<bool> MarkConsumedAsync(Guid accountId, int id, Guid characterId, CancellationToken cancellationToken = default)
    {
        await this.EnsureAvailableStorageAsync(cancellationToken).ConfigureAwait(false);

        await using var context = new CashShopContext();
        var now = DateTime.UtcNow;
        var updated = await context.StorageItems
            .Where(i => i.Id == id && i.AccountId == accountId && i.ConsumedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.ConsumedAt, now).SetProperty(i => i.CharacterId, characterId), cancellationToken)
            .ConfigureAwait(false);
        return updated == 1;
    }

    /// <inheritdoc />
    public async ValueTask UnmarkConsumedAsync(Guid accountId, int id, CancellationToken cancellationToken = default)
    {
        await this.EnsureAvailableStorageAsync(cancellationToken).ConfigureAwait(false);

        await using var context = new CashShopContext();
        await context.StorageItems
            .Where(i => i.Id == id && i.AccountId == accountId)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.ConsumedAt, (DateTime?)null).SetProperty(i => i.CharacterId, (Guid?)null), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the points of the account and locks its row until the end of the transaction.
    /// The row is created when the account never had points.
    /// </summary>
    private static async ValueTask<long> LockPointsAsync(CashShopContext context, Guid accountId, CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""INSERT INTO cashshop."Balance" ("AccountId", "Points", "UpdatedAt") VALUES ({accountId}, 0, now()) ON CONFLICT ("AccountId") DO NOTHING""",
            cancellationToken).ConfigureAwait(false);
        return await context.Database
            .SqlQuery<long>($"""SELECT "Points" AS "Value" FROM cashshop."Balance" WHERE "AccountId" = {accountId} FOR UPDATE""")
            .FirstAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SetPointsAsync(CashShopContext context, Guid accountId, long points, CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""UPDATE cashshop."Balance" SET "Points" = {points}, "UpdatedAt" = now() WHERE "AccountId" = {accountId}""",
            cancellationToken).ConfigureAwait(false);
    }

    private static CashShopTransaction CreateTransaction(Guid accountId, long amount, long pointsAfter, string reason, string? reference, string? comment)
    {
        return new CashShopTransaction
        {
            AccountId = accountId,
            Amount = amount,
            PointsAfter = pointsAfter,
            Reason = reason.Length > 32 ? reason[..32] : reason,
            Reference = reference,
            Comment = comment is { Length: > 256 } ? comment[..256] : comment,
            CreatedAt = DateTime.UtcNow,
        };
    }

    private async ValueTask EnsureAvailableStorageAsync(CancellationToken cancellationToken)
    {
        if (this._isStorageReady)
        {
            return;
        }

        if (DateTime.UtcNow < this._nextProbeAt)
        {
            throw new InvalidOperationException("The cash shop storage is not available. Please check the database connection.");
        }

        using var l = await this._storageLock.LockAsync(cancellationToken).ConfigureAwait(false);
        if (this._isStorageReady)
        {
            return;
        }

        try
        {
            await using var context = new CashShopContext();
            await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            this._isStorageReady = true;
        }
        catch (Exception ex)
        {
            this._nextProbeAt = DateTime.UtcNow + RetryDelay;
            this._logger.LogWarning(ex, "The cash shop storage is not available (yet).");
            throw new InvalidOperationException("The cash shop storage is not available. Please check the database connection.", ex);
        }
    }

    private ValueTask OnDatabaseInitializedAsync()
    {
        this._isStorageReady = false;
        return ValueTask.CompletedTask;
    }
}
