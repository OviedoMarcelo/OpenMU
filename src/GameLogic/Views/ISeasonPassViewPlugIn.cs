// <copyright file="ISeasonPassViewPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Views;

using MUnique.OpenMU.GameLogic.PlugIns.SeasonPass;

/// <summary>
/// Interface of a view whose client can show the season pass in a window.
/// </summary>
public interface ISeasonPassViewPlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows the season pass of the account: the season, the level, the experience and the rewards.
    /// It replaces what the client knew before.
    /// </summary>
    /// <param name="overview">The season pass.</param>
    ValueTask ShowSeasonPassAsync(SeasonPassOverview overview);
}
