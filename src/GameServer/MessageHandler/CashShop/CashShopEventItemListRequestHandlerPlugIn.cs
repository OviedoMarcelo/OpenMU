// <copyright file="CashShopEventItemListRequestHandlerPlugIn.cs" company="MUnique">
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
/// Handler for the request of the items of an event category of the cash shop, which has none.
/// </summary>
[PlugIn]
[Display(Name = "CashShopEventItemListRequestHandlerPlugIn", Description = "Handler for the request of the items of an event category of the cash shop, which has none.")]
[Guid("1A6F8B23-5E0C-4D74-B3A9-0C2E7D5B9F61")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(CashShopGroupHandlerPlugIn.GroupKey)]
internal class CashShopEventItemListRequestHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => CashShopEventItemListRequest.SubCode;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length < 4)
        {
            return;
        }

        await player.InvokeViewPlugInAsync<ICashShopViewPlugIn>(p => p.ShowEmptyEventItemListAsync()).ConfigureAwait(false);
    }
}
