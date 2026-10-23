// <copyright file="ClientFeaturesHandlerPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.Network.Packets.ClientToServer;
using MUnique.OpenMU.Network.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Handler for the <see cref="Network.Packets.ClientToServer.ClientFeatures"/> message, which tells the optional features of the client.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.ClientFeaturesHandlerPlugIn_Name), Description = nameof(PlugInResources.ClientFeaturesHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("4A7D2E95-1B6C-4F83-9E0A-3C5B8D7F2A14")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(ChatCommandGroupHandlerPlugIn.GroupKey)]
internal class ClientFeaturesHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => Network.Packets.ClientToServer.ClientFeatures.SubCode;

    /// <inheritdoc/>
    public ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length < Network.Packets.ClientToServer.ClientFeatures.Length)
        {
            return ValueTask.CompletedTask;
        }

        Network.Packets.ClientToServer.ClientFeatures message = packet;
        GameLogic.ClientFeatures.Set(player, (ClientFeature)message.Features);
        return ValueTask.CompletedTask;
    }
}
