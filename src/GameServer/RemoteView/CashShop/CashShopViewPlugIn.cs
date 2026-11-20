// <copyright file="CashShopViewPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.CashShop;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.Views.CashShop;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using MUnique.OpenMU.Network.PlugIns;
using MUnique.OpenMU.PlugIns;
using StorageItem = MUnique.OpenMU.Persistence.CashShop.CashShopStorageItem;

/// <summary>
/// The default implementation of the <see cref="ICashShopViewPlugIn"/> which is forwarding everything to the game client
/// with the messages of the cash shop (0xD2).
/// </summary>
[PlugIn]
[Display(Name = "Cash Shop View", Description = "Envía al cliente los mensajes de la tienda del juego (0xD2): saldo, compras, depósito y versión del catálogo.")]
[Guid("2B7E9D14-6C3A-4F85-9E01-7D4A8C2B5F63")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public class CashShopViewPlugIn : ICashShopViewPlugIn
{
    /// <summary>
    /// The type of the items of the storage which the client shows by their product ("P").
    /// </summary>
    private const byte ProductItemType = (byte)'P';

    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="CashShopViewPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public CashShopViewPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask ShowPointsAsync(long points)
    {
        // The client shows the "credit card cash" as the main points.
        await this._player.Connection.SendCashShopPointInfoAsync(0, points, points, 0, 0, 0).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask ShowOpenResultAsync(bool success)
    {
        await this._player.Connection.SendCashShopOpenResponseAsync(success).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask ShowBuyResultAsync(CashShopBuyResult result)
    {
        await this._player.Connection.SendCashShopItemBuyResponseAsync((byte)result, 0).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask ShowStorageAsync(IList<StorageItem> items, int totalCount, int page, int totalPages)
    {
        var connection = this._player.Connection;
        await connection.SendCashShopStorageItemCountAsync(Clamp(totalCount), Clamp(items.Count), Clamp(page), Clamp(totalPages)).ConfigureAwait(false);
        foreach (var item in items)
        {
            var id = (uint)item.Id;
            await connection.SendCashShopStorageItemAsync(id, id, 0, (uint)item.ProductSeq, (uint)item.PriceSeq, 0, ProductItemType).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask ShowConsumeResultAsync(CashShopConsumeResult result)
    {
        await this._player.Connection.SendCashShopStorageItemConsumeResponseAsync((byte)result).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask ShowCatalogVersionAsync(ushort salesZone, ushort year, ushort yearId)
    {
        await this._player.Connection.SendCashShopCatalogVersionAsync(salesZone, year, yearId).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask ShowEmptyEventItemListAsync()
    {
        await this._player.Connection.SendCashShopEventItemListCountAsync(0).ConfigureAwait(false);
    }

    private static ushort Clamp(int value) => (ushort)Math.Clamp(value, 0, ushort.MaxValue);
}
