// <copyright file="IPlayerTitleViewPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Views;

using MUnique.OpenMU.GameLogic.PlugIns.Achievements;

/// <summary>
/// Interface of a view whose client can show a title below the name of a player.
/// </summary>
public interface IPlayerTitleViewPlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows the title of a player below its name, or removes it.
    /// </summary>
    /// <param name="player">The player whose title is shown. It may be the player of this view.</param>
    /// <param name="title">The title; <c>null</c> to remove the title.</param>
    ValueTask ShowTitleAsync(Player player, TitleDefinition? title);
}
