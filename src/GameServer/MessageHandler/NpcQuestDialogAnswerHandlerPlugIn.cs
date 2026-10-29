// <copyright file="NpcQuestDialogAnswerHandlerPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlugIns.WeeklyQuests;
using MUnique.OpenMU.Network.Packets.ClientToServer;
using MUnique.OpenMU.Network.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Handler for the answer of the dialog of an NPC: of the ones whose talk is a step of a quest,
/// and of the questions of other NPCs (see <see cref="NpcDialogs"/>).
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.NpcQuestDialogAnswerHandlerPlugIn_Name), Description = nameof(PlugInResources.NpcQuestDialogAnswerHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("D2C85F17-6A3E-4B90-8F4D-1E7A2B9C5D36")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(ChatCommandGroupHandlerPlugIn.GroupKey)]
internal class NpcQuestDialogAnswerHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => NpcQuestDialogAnswer.SubCode;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length < NpcQuestDialogAnswer.Length)
        {
            return;
        }

        NpcQuestDialogAnswer message = packet;
        if (await NpcDialogs.TryAnswerAsync(player, message.DialogId, message.Accepted).ConfigureAwait(false)
            || WeeklyQuestsPlugIn.GetTrackingPlugIn(player) is not { } weeklyQuests)
        {
            return;
        }

        await weeklyQuests.NpcQuestDialogAnsweredAsync(player, message.DialogId, message.Accepted).ConfigureAwait(false);
    }
}
