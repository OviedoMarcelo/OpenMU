// <copyright file="BossEncounterResult.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.BossEncounters;

/// <summary>
/// The result of a finished boss encounter, for its history.
/// </summary>
/// <param name="Name">The name of the encounter.</param>
/// <param name="ServerId">The id of the game server; <c>null</c>, if it's unknown.</param>
/// <param name="MapName">The name of the map.</param>
/// <param name="StartUtc">The start, in UTC.</param>
/// <param name="Duration">The duration.</param>
/// <param name="Outcome">How the encounter ended.</param>
/// <param name="KillerName">The name of the character who killed the boss, if it was defeated.</param>
/// <param name="PhaseName">The name of the last phase; <c>null</c> without phases.</param>
/// <param name="Ranking">The players who damaged the boss, the one with the most damage first.</param>
public sealed record BossEncounterResult(
    string Name,
    int? ServerId,
    string MapName,
    DateTime StartUtc,
    TimeSpan Duration,
    BossEncounterOutcome Outcome,
    string? KillerName,
    string? PhaseName,
    IReadOnlyList<BossEncounterRankingEntry> Ranking);
