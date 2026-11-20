// <copyright file="CashShopItemBuyRequestHandlerPlugIn.cs" company="MUnique">
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
/// Handler for buying an item in the cash shop.
/// </summary>
[PlugIn]
[Display(Name = "CashShopItemBuyRequestHandlerPlugIn", Description = "Handler for buying an item in the cash shop.")]
[Guid("9B2E5C18-4D7A-4E63-8F01-6A3C9E2D7B54")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(CashShopGroupHandlerPlugIn.GroupKey)]
internal class CashShopItemBuyRequestHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => CashShopItemBuyRequest.SubCode;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length < 16)
        {
            return;
        }

        CashShopItemBuyRequest message = packet;
        if (CashShopPlugIn.GetActivePlugIn(player.GameContext) is { } cashShop)
        {
            // The client sends the number of the package and the one of its price (0, when the package has only one).
            await cashShop.BuyAsync(player, (int)message.PackageMainIndex, (int)message.ProductMainIndex).ConfigureAwait(false);
        }
        else
        {
            await player.InvokeViewPlugInAsync<ICashShopViewPlugIn>(p => p.ShowBuyResultAsync(CashShopBuyResult.NotAvailable)).ConfigureAwait(false);
        }
    }
}
