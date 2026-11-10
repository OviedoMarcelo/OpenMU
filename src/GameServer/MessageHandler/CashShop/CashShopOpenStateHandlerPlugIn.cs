// <copyright file="CashShopOpenStateHandlerPlugIn.cs" company="MUnique">
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
/// Handler for opening and closing the cash shop.
/// </summary>
[PlugIn]
[Display(Name = "CashShopOpenStateHandlerPlugIn", Description = "Handler for opening and closing the cash shop.")]
[Guid("6F1D4B92-8A3E-4C57-B0D6-2E9A7C4F1B35")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(CashShopGroupHandlerPlugIn.GroupKey)]
internal class CashShopOpenStateHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => CashShopOpenState.SubCode;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length < 5)
        {
            return;
        }

        CashShopOpenState message = packet;
        if (message.IsClosed)
        {
            return;
        }

        if (CashShopPlugIn.GetActivePlugIn(player.GameContext) is { } cashShop)
        {
            await cashShop.OpenAsync(player).ConfigureAwait(false);
        }
        else
        {
            await player.InvokeViewPlugInAsync<ICashShopViewPlugIn>(p => p.ShowOpenResultAsync(false)).ConfigureAwait(false);
        }
    }
}
