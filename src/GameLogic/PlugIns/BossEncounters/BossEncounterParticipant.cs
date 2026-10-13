// <copyright file="BossEncounterParticipant.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.BossEncounters;

/// <summary>
/// A player who damaged the boss of an encounter, or healed the players in its arena.
/// </summary>
/// <param name="Player">The player.</param>
/// <param name="Name">The name of the character.</param>
/// <param name="Damage">The damage which the player and its summons did to the boss.</param>
/// <param name="Percent">The percentage of the damage of all players.</param>
/// <param name="Healing">The health which the player restored to players in the arena.</param>
/// <param name="HealingPercent">The percentage of the healing of all players.</param>
public sealed record BossEncounterParticipant(Player Player, string Name, long Damage, double Percent, long Healing = 0, double HealingPercent = 0);
