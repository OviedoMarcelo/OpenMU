// <copyright file="CashShopStorageItemConsumeRequestHandlerPlugIn.cs" company="MUnique">
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
/// Handler for using an item of the storage of the cash shop.
/// </summary>
[PlugIn]
[Display(Name = "CashShopStorageItemConsumeRequestHandlerPlugIn", Description = "Handler for using an item of the storage of the cash shop.")]
[Guid("7D0C3E46-2F8B-4A19-9E57-1B6D4A8C3F02")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(CashShopGroupHandlerPlugIn.GroupKey)]
internal class CashShopStorageItemConsumeRequestHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => CashShopStorageItemConsumeRequest.SubCode;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length < 12)
        {
            return;
        }

        CashShopStorageItemConsumeRequest message = packet;
        if (CashShopPlugIn.GetActivePlugIn(player.GameContext) is { } cashShop)
        {
            await cashShop.ConsumeAsync(player, (int)Math.Min(message.BaseItemCode, int.MaxValue)).ConfigureAwait(false);
        }
    }
}
