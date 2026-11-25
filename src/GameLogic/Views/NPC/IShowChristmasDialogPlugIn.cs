// <copyright file="IShowChristmasDialogPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Views.NPC;

/// <summary>
/// Interface of a view whose implementation shows the dialogs of the NPCs of Santa Village.
/// </summary>
public interface IShowChristmasDialogPlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows the dialog of Santa Claus, in which the player asks for the gift.
    /// </summary>
    ValueTask ShowSantaClausDialogAsync();

    /// <summary>
    /// Shows the dialog of the Snowman, in which the player asks to go back to Devias.
    /// </summary>
    ValueTask ShowSnowmanDialogAsync();

    /// <summary>
    /// Tells the client whether it shows the Christmas version of Lorencia, the next time it loads the map.
    /// </summary>
    /// <param name="enabled">If set to <c>true</c>, the client shows the Christmas version of Lorencia.</param>
    ValueTask SetChristmasLorenciaAsync(bool enabled);
}
