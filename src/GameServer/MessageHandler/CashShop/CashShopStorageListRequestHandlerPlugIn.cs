// <copyright file="CashShopStorageListRequestHandlerPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.MessageHandler.CashShop;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlugIns.CashShop;
using MUnique.OpenMU.GameLogic.Views.CashShop;
using MUnique.OpenMU.Network.Packets.ClientToServer;
using MUnique.OpenMU.Network.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Handler for the request of a page of the storage of the cash shop.
/// </summary>
[PlugIn]
[Display(Name = "CashShopStorageListRequestHandlerPlugIn", Description = "Handler for the request of a page of the storage of the cash shop.")]
[Guid("4E7A0D35-9C1B-4B86-A2F4-8D5E3B6C0A17")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(CashShopGroupHandlerPlugIn.GroupKey)]
internal class CashShopStorageListRequestHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => CashShopStorageListRequest.SubCode;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length < 9)
        {
            return;
        }

        CashShopStorageListRequest message = packet;
        if (CashShopPlugIn.GetActivePlugIn(player.GameContext) is not { } cashShop)
        {
            return;
        }

        if (message.InventoryType == (byte)'G')
        {
            // The gift box: gifts are not supported, so it is always empty.
            await player.InvokeViewPlugInAsync<ICashShopViewPlugIn>(p => p.ShowStorageAsync([], 0, 0, 0)).ConfigureAwait(false);
            return;
        }

        await cashShop.SendStorageAsync(player, (int)Math.Min(message.PageIndex, int.MaxValue)).ConfigureAwait(false);
    }
}
