// <copyright file="IBossEncounterEndedPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns;

using System.Runtime.InteropServices;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// A plugin interface which is called when a boss encounter has been ended, e.g. to count it for quests.
/// </summary>
[Guid("92CA6738-C91E-4997-BDA4-C1E65AD6804E")]
[PlugInPoint("Boss encounter ended", "Plugins which will be executed when a boss encounter has been ended.")]
public interface IBossEncounterEndedPlugIn
{
    /// <summary>
    /// Is called when a boss encounter has been ended.
    /// </summary>
    /// <param name="encounterName">The name of the encounter.</param>
    /// <param name="isDefeated">If set to <c>true</c>, the boss has been defeated; otherwise it retreated or was stopped.</param>
    /// <param name="participants">The players who took part with at least the minimum share of the damage or the healing, and are still online.</param>
    ValueTask BossEncounterEndedAsync(string encounterName, bool isDefeated, IReadOnlyCollection<Player> participants);
}
