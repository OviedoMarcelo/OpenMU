// <copyright file="INpcQuestDialogViewPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Views.Quest;

using MUnique.OpenMU.GameLogic.NPC;

/// <summary>
/// Interface of a view whose client shows a dialog with the portrait of an NPC, what it says and
/// the buttons to accept or decline, when the talk to the NPC is a step of a quest.
/// </summary>
public interface INpcQuestDialogViewPlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows the dialog of the NPC.
    /// </summary>
    /// <param name="npc">The NPC.</param>
    /// <param name="dialogId">The id of the dialog, which the answer of the client refers to.</param>
    /// <param name="questName">The name of the quest.</param>
    /// <param name="text">What the NPC says. Lines are separated by a line feed.</param>
    ValueTask ShowNpcQuestDialogAsync(NonPlayerCharacter npc, uint dialogId, string questName, string text);
}
