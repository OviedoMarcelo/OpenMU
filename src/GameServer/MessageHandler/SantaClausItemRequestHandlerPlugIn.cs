// <copyright file="SantaClausItemRequestHandlerPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlugIns.ChristmasEvent;
using MUnique.OpenMU.Network.Packets.ClientToServer;
using MUnique.OpenMU.Network.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Handler for <see cref="SantaClausItemRequest"/> packets, which the client sends when the player accepts the
/// dialog of Santa Claus in Santa Village.
/// </summary>
[PlugIn]
[Display(Name = "Santa Claus Item Request Handler", Description = "Maneja el pedido del regalo de Santa Claus en Santa Village.")]
[Guid("E2A6C4D1-3B7F-4E9A-8C5D-1F3B5D7E9A2C")]
[MinimumClient(3, 0, ClientLanguage.Invariant)]
[BelongsToGroup(NpcActionGroupHandlerPlugIn.GroupKey)]
internal class SantaClausItemRequestHandlerPlugIn : ISubPacketHandlerPlugIn
{
    private readonly SantaClausGiftAction _giftAction = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => SantaClausItemRequest.SubCode;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        await this._giftAction.RequestGiftAsync(player).ConfigureAwait(false);
    }
}
