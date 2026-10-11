// <copyright file="BossEncounterParticipant.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.BossEncounters;

/// <summary>
/// A player who damaged the boss of an encounter.
/// </summary>
/// <param name="Player">The player.</param>
/// <param name="Name">The name of the character.</param>
/// <param name="Damage">The damage which the player and its summons did to the boss.</param>
/// <param name="Percent">The percentage of the damage of all players.</param>
public sealed record BossEncounterParticipant(Player Player, string Name, long Damage, double Percent);
