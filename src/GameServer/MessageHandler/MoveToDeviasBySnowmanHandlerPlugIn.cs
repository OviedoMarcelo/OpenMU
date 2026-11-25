// <copyright file="MoveToDeviasBySnowmanHandlerPlugIn.cs" company="MUnique">
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
/// Handler for <see cref="MoveToDeviasBySnowmanRequest"/> packets, which the client sends when the player accepts
/// the dialog of the Snowman in Santa Village.
/// </summary>
[PlugIn]
[Display(Name = "Move To Devias By Snowman Handler", Description = "Maneja el pedido de volver a Devias desde el Snowman de Santa Village.")]
[Guid("4F8B2D6A-9C1E-4B3D-A5F7-2E4C6A8B1D3F")]
[MinimumClient(3, 0, ClientLanguage.Invariant)]
[BelongsToGroup(NpcActionGroupHandlerPlugIn.GroupKey)]
internal class MoveToDeviasBySnowmanHandlerPlugIn : ISubPacketHandlerPlugIn
{
    private readonly SantaVillageWarpAction _warpAction = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => MoveToDeviasBySnowmanRequest.SubCode;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        await this._warpAction.WarpBackToDeviasAsync(player).ConfigureAwait(false);
    }
}
