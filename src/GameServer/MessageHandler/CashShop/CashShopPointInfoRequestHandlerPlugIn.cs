// <copyright file="CashShopPointInfoRequestHandlerPlugIn.cs" company="MUnique">
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
/// Handler for the request of the points of the cash shop.
/// </summary>
[PlugIn]
[Display(Name = "CashShopPointInfoRequestHandlerPlugIn", Description = "Handler for the request of the points of the cash shop.")]
[Guid("3C8E1A57-0B4D-4F92-A6E3-9D7B2C5F1E84")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(CashShopGroupHandlerPlugIn.GroupKey)]
internal class CashShopPointInfoRequestHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => CashShopPointInfoRequest.SubCode;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length < 4)
        {
            return;
        }

        if (CashShopPlugIn.GetActivePlugIn(player.GameContext) is { } cashShop)
        {
            await cashShop.SendPointsAsync(player).ConfigureAwait(false);
        }
    }
}
