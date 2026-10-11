// <copyright file="BossEncounterDamageTracker.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.BossEncounters;

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using MUnique.OpenMU.GameLogic.NPC;

/// <summary>
/// Sums up the damage which each player did to the boss of an encounter. The damage of summoned
/// monsters counts for their owner.
/// </summary>
/// <remarks>
/// The boss is registered with its tracker, so that a hit finds it without searching the encounters,
/// as hits are frequent.
/// </remarks>
public sealed class BossEncounterDamageTracker
{
    private static readonly ConditionalWeakTable<Monster, BossEncounterDamageTracker> Trackers = new();

    private readonly ConcurrentDictionary<Player, long> _damage = new();

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
    /// Forgets the damage, e.g. when the encounter starts again.
    /// </summary>
    public void Clear()
    {
        this._damage.Clear();
    }

    /// <summary>
    /// Gets the players who damaged the boss, the one with the most damage first.
    /// </summary>
    /// <returns>The participants.</returns>
    public IReadOnlyList<BossEncounterParticipant> GetParticipants()
    {
        var damages = this._damage.ToArray();
        var total = damages.Sum(entry => entry.Value);
        if (total <= 0)
        {
            return [];
        }

        return damages
            .OrderByDescending(entry => entry.Value)
            .Select(entry => new BossEncounterParticipant(entry.Key, entry.Key.Name, entry.Value, entry.Value * 100.0 / total))
            .ToList();
    }
}
