// <copyright file="BossEncounterSnapshot.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.BossEncounters;

/// <summary>
/// The state of a boss encounter at a point in time, e.g. for the admin panel.
/// </summary>
/// <param name="Name">The name of the encounter.</param>
/// <param name="IsActive">A value indicating whether the encounter starts by its schedule.</param>
/// <param name="IsRunning">A value indicating whether the encounter is running.</param>
/// <param name="NextStart">The next start in the time zone of the server, if it's planned.</param>
/// <param name="StartUtc">The start of the running encounter, in UTC.</param>
/// <param name="MapName">The name of the map of the running encounter.</param>
/// <param name="X">The x coordinate where the boss appeared.</param>
/// <param name="Y">The y coordinate where the boss appeared.</param>
/// <param name="PhaseName">The name of the current phase.</param>
/// <param name="HealthPercent">The health of the boss in percent.</param>
/// <param name="PlayersInArena">The number of living players in the arena.</param>
public sealed record BossEncounterSnapshot(
    string Name,
    bool IsActive,
    bool IsRunning,
    DateTime? NextStart,
    DateTime? StartUtc,
    string? MapName,
    int? X,
    int? Y,
    string? PhaseName,
    double? HealthPercent,
    int PlayersInArena);
