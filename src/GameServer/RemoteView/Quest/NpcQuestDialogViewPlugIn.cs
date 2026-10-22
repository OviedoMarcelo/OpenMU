// <copyright file="NpcQuestDialogViewPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Quest;

using System.Runtime.InteropServices;
using System.Text;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.Views.Quest;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using MUnique.OpenMU.Network.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The default implementation of the <see cref="INpcQuestDialogViewPlugIn"/> which sends a <see cref="NpcQuestDialog"/> message.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.NpcQuestDialogViewPlugIn_Name), Description = nameof(PlugInResources.NpcQuestDialogViewPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("8E3B6D41-2C7A-4F19-B5D0-7A9E1C4F6B23")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public class NpcQuestDialogViewPlugIn : INpcQuestDialogViewPlugIn
{
    /// <summary>
    /// The maximum size of the text in bytes. More wouldn't fit into the dialog anyway.
    /// </summary>
    private const int MaximumTextBytes = 2000;

    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="NpcQuestDialogViewPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public NpcQuestDialogViewPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public ValueTask ShowNpcQuestDialogAsync(NonPlayerCharacter npc, uint dialogId, string questName, string text)
    {
        if (this._player.Connection is not { Connected: true } connection)
        {
            return ValueTask.CompletedTask;
        }

        var npcId = npc.Id;
        var npcName = Fit(npc.Definition.Designation, 32);
        questName = Fit(questName, 48);
        text = Fit(text, MaximumTextBytes);

        int Write()
        {
            var size = NpcQuestDialogRef.GetRequiredSize(text);
            var span = connection.Output.GetSpan(size)[..size];
            _ = new NpcQuestDialogRef(span)
            {
                NpcId = npcId,
                NpcNumber = (ushort)npc.Definition.Number,
                DialogId = dialogId,
                NpcName = npcName,
                QuestName = questName,
                Text = text,
            };

            return size;
        }

        return connection.SendAsync(Write);
    }

    /// <summary>
    /// Cuts the text, so that it fits into a string field of the specified size, without cutting a character in half.
    /// </summary>
    private static string Fit(string? text, int maxBytes)
    {
        text ??= string.Empty;
        if (Encoding.UTF8.GetByteCount(text) <= maxBytes)
        {
            return text;
        }

        var length = Math.Min(text.Length, maxBytes);
        while (length > 0 && Encoding.UTF8.GetByteCount(text.AsSpan(0, length)) > maxBytes)
        {
            length--;
        }

        if (length > 0 && char.IsHighSurrogate(text[length - 1]))
        {
            length--;
        }

        return text[..length];
    }
}
