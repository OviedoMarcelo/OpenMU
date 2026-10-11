// <copyright file="BossEncounterDamageTracker.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.BossEncounters;

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using MUnique.OpenMU.GameLogic.NPC;

/// <summary>
/// Sums up the damage which each player did to the boss of an encounter, and the health which each
/// player restored to the players in its arena. The damage of summoned monsters counts for their owner.
/// </summary>
/// <remarks>
/// The boss is registered with its tracker, so that a hit finds it without searching the encounters,
/// as hits are frequent.
/// </remarks>
public sealed class BossEncounterDamageTracker
{
    private static readonly ConditionalWeakTable<Monster, BossEncounterDamageTracker> Trackers = new();

    private readonly ConcurrentDictionary<Player, long> _damage = new();
    private readonly ConcurrentDictionary<Player, long> _healing = new();

    /// <summary>
    /// Gets the tracker of the boss which was hit.
    /// </summary>
    /// <param name="attackable">The object which was hit.</param>
    /// <returns>The tracker; <c>null</c>, if the object isn't the boss of a running encounter.</returns>
    public static BossEncounterDamageTracker? Get(IAttackable attackable)
    {
        return attackable is Monster monster && Trackers.TryGetValue(monster, out var tracker) ? tracker : null;
    }

    /// <summary>
    /// Registers the boss with this tracker, so that its hits are tracked.
    /// </summary>
    /// <param name="boss">The boss.</param>
    public void Track(Monster boss)
    {
        Trackers.AddOrUpdate(boss, this);
    }

    /// <summary>
    /// Stops tracking the hits of the boss.
    /// </summary>
    /// <param name="boss">The boss.</param>
    public void Untrack(Monster boss)
    {
        Trackers.Remove(boss);
    }

    /// <summary>
    /// Adds the damage of a hit.
    /// </summary>
    /// <param name="attacker">The attacker.</param>
    /// <param name="damage">The damage.</param>
    public void AddDamage(IAttacker attacker, long damage)
    {
        var player = attacker as Player ?? (attacker as Monster)?.SummonedBy;
        if (player is null || damage <= 0)
        {
            return;
        }

        this._damage.AddOrUpdate(player, damage, (_, sum) => sum + damage);
    }

    /// <summary>
    /// Adds the health which a player restored to a player in the arena.
    /// </summary>
    /// <param name="healer">The player who healed.</param>
    /// <param name="amount">The restored health.</param>
    public void AddHealing(Player healer, long amount)
    {
        if (amount <= 0)
        {
            return;
        }

        this._healing.AddOrUpdate(healer, amount, (_, sum) => sum + amount);
    }

    /// <summary>
    /// Forgets the damage and the healing, e.g. when the encounter starts again.
    /// </summary>
    public void Clear()
    {
        this._damage.Clear();
        this._healing.Clear();
    }

    /// <summary>
    /// Gets the players who damaged the boss or healed, the one with the most damage first.
    /// </summary>
    /// <returns>The participants.</returns>
    public IReadOnlyList<BossEncounterParticipant> GetParticipants()
    {
        var damages = this._damage.ToArray().ToDictionary(entry => entry.Key, entry => entry.Value);
        var healings = this._healing.ToArray().ToDictionary(entry => entry.Key, entry => entry.Value);
        var totalDamage = damages.Values.Sum();
        var totalHealing = healings.Values.Sum();

        return damages.Keys.Union(healings.Keys)
            .Select(player =>
            {
                var damage = damages.GetValueOrDefault(player);
                var healing = healings.GetValueOrDefault(player);
                return new BossEncounterParticipant(
                    player,
                    player.Name,
                    damage,
                    totalDamage > 0 ? damage * 100.0 / totalDamage : 0,
                    healing,
                    totalHealing > 0 ? healing * 100.0 / totalHealing : 0);
            })
            .OrderByDescending(p => p.Damage)
            .ThenByDescending(p => p.Healing)
            .ToList();
    }
}
