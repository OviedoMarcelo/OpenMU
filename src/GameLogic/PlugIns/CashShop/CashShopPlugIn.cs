// <copyright file="CashShopPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.CashShop;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic.PlugIns.Cosmetics;
using MUnique.OpenMU.GameLogic.Views.CashShop;
using MUnique.OpenMU.GameLogic.Views.Inventory;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.CashShop;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The cash shop of the game (the "X" window of the client): the player spends its currency (e.g. "Cyber Coins",
/// which it gets for referrals) on items, which wait in the storage of the shop until the player uses them.
/// </summary>
/// <remarks>
/// The client shows the catalog from files which it downloads (see <see cref="CashShopCatalogFiles"/>);
/// the server announces their version when the player entered the game.
/// </remarks>
[PlugIn]
[Display(Name = "Cash Shop", Description = "La tienda del juego (tecla X): los jugadores gastan sus Cyber Coins en ítems y skins, que esperan en el depósito de la tienda hasta que los canjean. Los productos se editan en mu-admin (Tienda).")]
[Guid("8E4B2C61-5F0A-4D39-B7E2-3A9C1D6F0E47")]
public class CashShopPlugIn : IPlayerStateChangedPlugIn, ISupportCustomConfiguration<CashShopConfiguration>, ISupportDefaultCustomConfiguration, IDisabledByDefault
{
    private static readonly InMemoryCashShopRepository FallbackRepository = new();

    private readonly ICashShopRepository? _repository;
    private readonly object _catalogLock = new();
    private CashShopConfiguration? _configuration;
    private (CashShopCatalog Catalog, CashShopCatalogFiles Files)? _catalog;

    /// <summary>
    /// Initializes a new instance of the <see cref="CashShopPlugIn"/> class,
    /// which uses the repository of the <see cref="CashShopRepositoryRegistry"/>.
    /// </summary>
    public CashShopPlugIn()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CashShopPlugIn"/> class.
    /// </summary>
    /// <remarks>
    /// It's internal on purpose: the plugin manager must only see the parameterless constructor. Otherwise it resolves
    /// the repository at the start, which needs the plugin configurations which are just being created, and hangs.
    /// </remarks>
    /// <param name="repository">The repository.</param>
    internal CashShopPlugIn(ICashShopRepository repository)
    {
        this._repository = repository;
    }

    /// <inheritdoc />
    public CashShopConfiguration? Configuration
    {
        get => this._configuration;
        set
        {
            lock (this._catalogLock)
            {
                this._configuration = value;
                this._catalog = null;
            }
        }
    }

    /// <summary>
    /// Gets the repository.
    /// </summary>
    internal ICashShopRepository Repository => this._repository ?? CashShopRepositoryRegistry.Current ?? FallbackRepository;

    /// <summary>
    /// Gets the active plugin of the game.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The plugin; <c>null</c>, if it's not active.</returns>
    public static CashShopPlugIn? GetActivePlugIn(IGameContext gameContext)
    {
        return gameContext.PlugInManager.GetActivePlugInsOf<IPlayerStateChangedPlugIn>().OfType<CashShopPlugIn>().FirstOrDefault();
    }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new CashShopConfiguration();

    /// <inheritdoc />
    public ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState) => ValueTask.CompletedTask;

    /// <summary>
    /// Gets the catalog and its files. They're built once per configuration.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The catalog and its files.</returns>
    public (CashShopCatalog Catalog, CashShopCatalogFiles Files) GetCatalog(IGameContext gameContext)
    {
        lock (this._catalogLock)
        {
            if (this._catalog is { } cached)
            {
                return cached;
            }

            var configuration = this._configuration ?? new CashShopConfiguration();
            var catalog = CashShopCatalog.Build(configuration, gameContext.Configuration.Items);
            var result = (catalog, CashShopCatalogFiles.Create(catalog, configuration.SalesZone));
            this._catalog = result;
            return result;
        }
    }

    /// <summary>
    /// Tells the client the version of the catalog, so that it downloads it when needed.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The task.</returns>
    public async ValueTask SendCatalogVersionAsync(Player player)
    {
        var files = this.GetCatalog(player.GameContext).Files;
        await player.InvokeViewPlugInAsync<ICashShopViewPlugIn>(p => p.ShowCatalogVersionAsync(files.SalesZone, files.Year, files.YearId)).ConfigureAwait(false);
    }

    /// <summary>
    /// Opens the shop for the player.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The task.</returns>
    public async ValueTask OpenAsync(Player player)
    {
        var canOpen = player.SelectedCharacter is not null && player.Account is not null && player.PlayerState.CurrentState == PlayerState.EnteredWorld;
        await player.InvokeViewPlugInAsync<ICashShopViewPlugIn>(p => p.ShowOpenResultAsync(canOpen)).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends the points of the account of the player.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The task.</returns>
    public async ValueTask SendPointsAsync(Player player)
    {
        if (player.Account is not { } account)
        {
            return;
        }

        long points;
        try
        {
            points = await this.Repository.GetPointsAsync(account.GetId()).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "Couldn't load the cash shop points of account {account}.", account.LoginName);
            return;
        }

        await player.InvokeViewPlugInAsync<ICashShopViewPlugIn>(p => p.ShowPointsAsync(points)).ConfigureAwait(false);
    }

    /// <summary>
    /// Buys a variant of a package, which is then in the storage of the account.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="packageSeq">The number of the package.</param>
    /// <param name="priceSeq">The number of the variant; 0 for a package with one variant.</param>
    /// <returns>The result.</returns>
    public async ValueTask<CashShopBuyResult> BuyAsync(Player player, int packageSeq, int priceSeq)
    {
        var result = await this.TryBuyAsync(player, packageSeq, priceSeq).ConfigureAwait(false);
        await player.InvokeViewPlugInAsync<ICashShopViewPlugIn>(p => p.ShowBuyResultAsync(result)).ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// Sends a page of the storage of the account.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="page">The page, starting at 1.</param>
    /// <returns>The task.</returns>
    public async ValueTask SendStorageAsync(Player player, int page)
    {
        if (player.Account is not { } account)
        {
            return;
        }

        var pageSize = Math.Max(1, this.Configuration?.StoragePageSize ?? 7);
        try
        {
            page = Math.Max(1, page);
            var (items, total) = await this.Repository.LoadStorageAsync(account.GetId(), (page - 1) * pageSize, pageSize).ConfigureAwait(false);
            var totalPages = (total + pageSize - 1) / pageSize;
            if (items.Count == 0 && totalPages > 0 && page > totalPages)
            {
                // E.g. the last item of the last page was used.
                page = totalPages;
                (items, total) = await this.Repository.LoadStorageAsync(account.GetId(), (page - 1) * pageSize, pageSize).ConfigureAwait(false);
                totalPages = (total + pageSize - 1) / pageSize;
            }

            var shownPage = totalPages == 0 ? 0 : page;
            await player.InvokeViewPlugInAsync<ICashShopViewPlugIn>(p => p.ShowStorageAsync(items, total, shownPage, totalPages)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "Couldn't load the cash shop storage of account {account}.", account.LoginName);
        }
    }

    /// <summary>
    /// Uses an item of the storage: it's moved into the inventory of the character.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="storageItemId">The id of the item of the storage.</param>
    /// <returns>The result.</returns>
    public async ValueTask<CashShopConsumeResult> ConsumeAsync(Player player, int storageItemId)
    {
        var result = await this.TryConsumeAsync(player, storageItemId).ConfigureAwait(false);
        await player.InvokeViewPlugInAsync<ICashShopViewPlugIn>(p => p.ShowConsumeResultAsync(result)).ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// Gives points to an account, e.g. by a game master. A player of the account who is online sees the new points.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <param name="accountId">The id of the account.</param>
    /// <param name="amount">The amount; negative to take points.</param>
    /// <param name="reason">The reason.</param>
    /// <param name="comment">A comment, e.g. the name of the game master.</param>
    /// <returns>The points after the change; <c>null</c>, if a negative amount exceeds the points.</returns>
    public async ValueTask<long?> AddPointsAsync(IGameContext gameContext, Guid accountId, long amount, string reason, string? comment)
    {
        var points = await this.Repository.AddPointsAsync(accountId, amount, reason, null, comment).ConfigureAwait(false);
        if (points is not { } newPoints)
        {
            return null;
        }

        var players = new List<Player>();
        await gameContext.ForEachPlayerAsync(p =>
        {
            if (p.Account?.GetId() == accountId)
            {
                players.Add(p);
            }

            return Task.CompletedTask;
        }).ConfigureAwait(false);
        foreach (var player in players)
        {
            await player.InvokeViewPlugInAsync<ICashShopViewPlugIn>(p => p.ShowPointsAsync(newPoints)).ConfigureAwait(false);
        }

        return newPoints;
    }

    private static List<TemporaryItem> CreateItems(ItemDefinition definition, CashShopStorageItem stored)
    {
        var first = ConfiguredItemFactory.Create(definition, stored.ItemLevel, stored.Skill, stored.Luck, stored.OptionLevel, stored.ExcellentOptions);
        if (first.IsStackable())
        {
            first.Durability = Math.Clamp(stored.Quantity, (byte)1, definition.Durability > 0 ? definition.Durability : byte.MaxValue);
            return [first];
        }

        var items = new List<TemporaryItem> { first };
        for (var i = 1; i < stored.Quantity; i++)
        {
            items.Add(ConfiguredItemFactory.Create(definition, stored.ItemLevel, stored.Skill, stored.Luck, stored.OptionLevel, stored.ExcellentOptions));
        }

        return items;
    }

    private static bool HasSpaceFor(IStorage inventory, IReadOnlyList<TemporaryItem> items)
    {
        if (items.Count == 1)
        {
            return inventory.CheckInvSpace(items[0]) is not null;
        }

        // Several items: a rough check, which the adding itself confirms.
        return items.All(item => inventory.CheckInvSpace(item) is not null);
    }

    private static string TrimName(string name) => name.Length > 100 ? name[..100] : name;

    private static IReadOnlyList<CosmeticSlot> GetSkinSlots(byte itemGroup)
    {
        // An armor piece stands for the whole set; wings and pets are a single piece.
        return CosmeticSlotExtensions.ArmorSlots.Any(s => s.GetItemGroup() == itemGroup)
            ? CosmeticSlotExtensions.ArmorSlots
            : CosmeticSlotExtensions.All.Where(s => s.GetItemGroup() == itemGroup).ToList();
    }

    private async ValueTask<CashShopBuyResult> TryBuyAsync(Player player, int packageSeq, int priceSeq)
    {
        if (player.Account is not { } account || player.SelectedCharacter is null)
        {
            return CashShopBuyResult.CannotBuy;
        }

        if (this.GetCatalog(player.GameContext).Catalog.FindVariant(packageSeq, priceSeq) is not var (package, variant))
        {
            return CashShopBuyResult.NotAvailable;
        }

        var item = new CashShopStorageItem
        {
            AccountId = account.GetId(),
            PackageSeq = package.Seq,
            PriceSeq = variant.PriceSeq,
            ProductSeq = package.ProductSeq,
            Price = variant.Price,
            Name = TrimName(package.Variants.Count == 1 ? package.Name : $"{package.Name} ({variant.Name})"),
            ItemGroup = variant.Item.Definition.Group,
            ItemNumber = variant.Item.Definition.Number,
            ItemLevel = variant.Item.Level,
            Quantity = variant.Item.Quantity,
            Skill = variant.Item.Skill,
            Luck = variant.Item.Luck,
            OptionLevel = variant.Item.OptionLevel,
            ExcellentOptions = variant.Item.ExcellentOptions,
            IsSkin = variant.Item.IsSkin,
            PurchasedAt = DateTime.UtcNow,
        };

        try
        {
            var result = await this.Repository.PurchaseAsync(item, Math.Max(1, this.Configuration?.MaximumStorageItems ?? 50)).ConfigureAwait(false);
            switch (result)
            {
                case CashShopPurchaseResult.Success:
                    player.Logger.LogInformation("Account {account} bought \"{item}\" (package {package}, price {price}) for {points} cash shop points, storage item {id}.", account.LoginName, item.Name, package.Seq, variant.PriceSeq, variant.Price, item.Id);
                    return CashShopBuyResult.Success;
                case CashShopPurchaseResult.StorageFull:
                    return CashShopBuyResult.StorageFull;
                default:
                    return CashShopBuyResult.NotEnoughPoints;
            }
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "Account {account} couldn't buy package {package}, price {price} of the cash shop.", account.LoginName, packageSeq, priceSeq);
            return CashShopBuyResult.Error;
        }
    }

    private async ValueTask<CashShopConsumeResult> TryConsumeAsync(Player player, int storageItemId)
    {
        if (player.Account is not { } account || player.SelectedCharacter is not { } character || player.Inventory is not { } inventory)
        {
            return CashShopConsumeResult.CannotUse;
        }

        CashShopStorageItem? stored;
        try
        {
            stored = await this.Repository.GetStorageItemAsync(account.GetId(), storageItemId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "Couldn't load the cash shop storage item {id} of account {account}.", storageItemId, account.LoginName);
            return CashShopConsumeResult.Error;
        }

        if (stored is null)
        {
            return CashShopConsumeResult.NotFound;
        }

        if (stored.IsSkin)
        {
            return await this.TryUnlockSkinAsync(player, account, character, stored).ConfigureAwait(false);
        }

        var definition = player.GameContext.Configuration.Items.FirstOrDefault(i => i.Group == stored.ItemGroup && i.Number == stored.ItemNumber);
        if (definition is null)
        {
            player.Logger.LogWarning("The cash shop storage item {id} of account {account} refers to the unknown item {group}/{number}.", stored.Id, account.LoginName, stored.ItemGroup, stored.ItemNumber);
            return CashShopConsumeResult.CannotUse;
        }

        var items = CreateItems(definition, stored);
        if (!HasSpaceFor(inventory, items))
        {
            return CashShopConsumeResult.NoInventorySpace;
        }

        // The item is marked first, so that it can never be handed out twice.
        if (!await this.Repository.MarkConsumedAsync(account.GetId(), stored.Id, character.Id).ConfigureAwait(false))
        {
            return CashShopConsumeResult.NotFound;
        }

        var added = new List<Item>();
        foreach (var temporaryItem in items)
        {
            var item = temporaryItem.MakePersistent(player.PersistenceContext);
            if (!await inventory.AddItemAsync(item).ConfigureAwait(false))
            {
                await player.PersistenceContext.DeleteAsync(item).ConfigureAwait(false);
                foreach (var addedItem in added)
                {
                    await inventory.RemoveItemAsync(addedItem).ConfigureAwait(false);
                    await player.PersistenceContext.DeleteAsync(addedItem).ConfigureAwait(false);
                }

                await this.Repository.UnmarkConsumedAsync(account.GetId(), stored.Id).ConfigureAwait(false);
                return CashShopConsumeResult.NoInventorySpace;
            }

            added.Add(item);
        }

        foreach (var item in added)
        {
            await player.InvokeViewPlugInAsync<IItemAppearPlugIn>(p => p.ItemAppearAsync(item)).ConfigureAwait(false);
        }

        // The storage item is used already, so the new items must not get lost by a crash.
        await player.SaveProgressAsync().ConfigureAwait(false);
        player.Logger.LogInformation("Character {character} of account {account} got the cash shop storage item {id} \"{item}\".", character.Name, account.LoginName, stored.Id, stored.Name);
        return CashShopConsumeResult.Success;
    }

    /// <summary>
    /// Unlocks the skin of a storage item for the character: all pieces of an armor set, or the skin of wings or a pet.
    /// The pieces which the character has already stay as they are; when it has all of them, the item stays in the storage.
    /// </summary>
    private async ValueTask<CashShopConsumeResult> TryUnlockSkinAsync(Player player, Account account, Character character, CashShopStorageItem stored)
    {
        if (CosmeticsPlugIn.GetActivePlugIn(player) is not { Configuration: { } configuration } cosmetics)
        {
            await player.ShowBlueMessageAsync("Las skins no están disponibles ahora. La skin queda en el depósito de la tienda.").ConfigureAwait(false);
            return CashShopConsumeResult.CannotUse;
        }

        var slots = GetSkinSlots(stored.ItemGroup);
        var pieceNumbers = configuration.Pieces
            .Where(p => p.Number > 0 && p.ItemNumber == stored.ItemNumber && slots.Contains(p.Slot))
            .Select(p => p.Number)
            .Distinct()
            .ToList();
        if (pieceNumbers.Count == 0)
        {
            player.Logger.LogWarning("The cash shop storage item {id} of account {account} is the skin of item {group}/{number}, which has no cosmetic pieces.", stored.Id, account.LoginName, stored.ItemGroup, stored.ItemNumber);
            await player.ShowBlueMessageAsync("Esta skin no existe en el servidor. Queda en el depósito de la tienda.").ConfigureAwait(false);
            return CashShopConsumeResult.CannotUse;
        }

        if (await cosmetics.GetPiecesAsync(player).ConfigureAwait(false) is not { } entries)
        {
            return CashShopConsumeResult.Error;
        }

        var unlocked = entries.Where(e => e.IsUnlocked).Select(e => e.Definition.Number).ToHashSet();
        var missing = pieceNumbers.Where(n => !unlocked.Contains(n)).ToList();
        if (missing.Count == 0)
        {
            await player.ShowBlueMessageAsync("Este personaje ya tiene esa skin. Canjeala con otro personaje de la cuenta.").ConfigureAwait(false);
            return CashShopConsumeResult.CannotUse;
        }

        // The item is marked first, so that it can never be used twice.
        if (!await this.Repository.MarkConsumedAsync(account.GetId(), stored.Id, character.Id).ConfigureAwait(false))
        {
            return CashShopConsumeResult.NotFound;
        }

        foreach (var pieceNumber in missing)
        {
            if (await cosmetics.GrantAsync(player, pieceNumber, $"cashshop:{stored.Id}").ConfigureAwait(false) is null)
            {
                // The pieces which were unlocked stay unlocked; using the item again unlocks the rest.
                await this.Repository.UnmarkConsumedAsync(account.GetId(), stored.Id).ConfigureAwait(false);
                return CashShopConsumeResult.Error;
            }
        }

        player.Logger.LogInformation("Character {character} of account {account} unlocked the skin of the cash shop storage item {id} \"{item}\".", character.Name, account.LoginName, stored.Id, stored.Name);
        return CashShopConsumeResult.Success;
    }
}
