// <copyright file="NpcDialogs.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic;

using System.Runtime.CompilerServices;
using System.Threading;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.Views.Quest;

/// <summary>
/// Shows questions of NPCs in the dialog of the client which the quests use (see <see cref="INpcQuestDialogViewPlugIn"/>),
/// e.g. "Do you want to extend your vault?", and calls back when the player accepts.
/// </summary>
/// <remarks>
/// The quests have dialog ids of their own, which count up from 1. The ids of these dialogs have the highest bit set,
/// so that an answer never belongs to both.
/// </remarks>
public static class NpcDialogs
{
    /// <summary>
    /// The maximum distance between the player and the NPC, in tiles, when the player answers. Like the one of the quests.
    /// </summary>
    public const int MaximumDistance = 10;

    private const uint IdFlag = 0x8000_0000;

    private static readonly ConditionalWeakTable<Player, OpenedDialog> OpenedDialogs = new();

    private static int _lastId;

    /// <summary>
    /// Determines whether the client of the player can show the dialog.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns><c>true</c>, if the client can show it.</returns>
    public static bool IsSupported(Player player) => ClientFeatures.Has(player, ClientFeature.NpcQuestDialog);

    /// <summary>
    /// Shows a question of the NPC. It replaces the dialog which the player had open.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="npc">The NPC.</param>
    /// <param name="title">The title, e.g. "Extended vault".</param>
    /// <param name="text">The text.</param>
    /// <param name="onAccepted">Is called when the player accepts, while still next to the NPC.</param>
    public static async ValueTask ShowAsync(Player player, NonPlayerCharacter npc, string title, string text, Func<Player, NonPlayerCharacter, ValueTask> onAccepted)
    {
        var id = IdFlag | ((uint)Interlocked.Increment(ref _lastId) & ~IdFlag);
        OpenedDialogs.AddOrUpdate(player, new OpenedDialog(id, npc, onAccepted));
        await player.InvokeViewPlugInAsync<INpcQuestDialogViewPlugIn>(p => p.ShowNpcQuestDialogAsync(npc, id, title, text)).ConfigureAwait(false);
    }

    /// <summary>
    /// Handles the answer of the player to a dialog, if it's one of these.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="dialogId">The id of the dialog.</param>
    /// <param name="accepted">If set, the player accepted.</param>
    /// <returns><c>true</c>, if the dialog was one of these; otherwise it may be one of the quests.</returns>
    public static async ValueTask<bool> TryAnswerAsync(Player player, uint dialogId, bool accepted)
    {
        if ((dialogId & IdFlag) == 0)
        {
            return false;
        }

        if (!OpenedDialogs.TryGetValue(player, out var dialog) || dialog.Id != dialogId)
        {
            return true;
        }

        OpenedDialogs.Remove(player);
        var npc = dialog.Npc;
        if (!accepted)
        {
            return true;
        }

        // The player may have walked away or left the map before answering.
        if (npc.CurrentMap is null || player.CurrentMap != npc.CurrentMap || !player.IsInRange(npc, MaximumDistance))
        {
            return true;
        }

        await dialog.OnAccepted(player, npc).ConfigureAwait(false);
        return true;
    }

    private sealed record OpenedDialog(uint Id, NonPlayerCharacter Npc, Func<Player, NonPlayerCharacter, ValueTask> OnAccepted);
}
