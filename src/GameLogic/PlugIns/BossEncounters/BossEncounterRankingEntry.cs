// <copyright file="BossEncounterRankingEntry.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.BossEncounters;

/// <summary>
/// A player in the rankings of a finished boss encounter.
/// </summary>
/// <param name="Name">The name of the character.</param>
/// <param name="Damage">The damage which the player and its summons did to the boss.</param>
/// <param name="Percent">The percentage of the damage of all players.</param>
/// <param name="WasRewarded">A value indicating whether the player got a reward for the participation or its place.</param>
/// <param name="Healing">The health which the player restored to players in the arena.</param>
public sealed record BossEncounterRankingEntry(string Name, long Damage, double Percent, bool WasRewarded, long Healing = 0);
