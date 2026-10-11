// <copyright file="BossEncounterRankingEntry.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.BossEncounters;

/// <summary>
/// A player in the ranking of the damage of a finished boss encounter.
/// </summary>
/// <param name="Name">The name of the character.</param>
/// <param name="Damage">The damage which the player and its summons did to the boss.</param>
/// <param name="Percent">The percentage of the damage of all players.</param>
/// <param name="WasRewarded">A value indicating whether the player got the reward for the participation.</param>
public sealed record BossEncounterRankingEntry(string Name, long Damage, double Percent, bool WasRewarded);
