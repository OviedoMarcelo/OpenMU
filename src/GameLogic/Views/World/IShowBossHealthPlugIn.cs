// <copyright file="IShowBossHealthPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Views.World;

using MUnique.OpenMU.GameLogic.NPC;

/// <summary>
/// Interface of a view whose client can show the health of a boss in a bar, e.g. of a boss encounter.
/// </summary>
public interface IShowBossHealthPlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows or updates the bar of the health of the boss.
    /// </summary>
    /// <param name="boss">The boss.</param>
    /// <param name="bossName">The name of the boss.</param>
    /// <param name="phaseName">The name of the current phase; empty without phases.</param>
    /// <param name="healthPercent">The health in percent of the maximum health, 0 to 100.</param>
    ValueTask ShowBossHealthAsync(NonPlayerCharacter boss, string bossName, string phaseName, byte healthPercent);

    /// <summary>
    /// Hides the bar of the health of the boss.
    /// </summary>
    /// <param name="boss">The boss.</param>
    ValueTask HideBossHealthAsync(NonPlayerCharacter boss);
}
