// <copyright file="BossEncounterHitPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.BossEncounters;

using System.Runtime.InteropServices;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Tracks the damage which the players do to the bosses of the encounters, for the rewards for the
/// participation and the ranking. It does nothing while no encounter runs.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.BossEncounterHitPlugIn_Name), Description = nameof(PlugInResources.BossEncounterHitPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("6F1B0E59-6C0A-4B8E-9E43-2A7C51D9B0E4")]
public sealed class BossEncounterHitPlugIn : IAttackableGotHitPlugIn
{
    /// <inheritdoc />
    public void AttackableGotHit(IAttackable attackable, IAttacker attacker, HitInfo hitInfo)
    {
        BossEncounterDamageTracker.Get(attackable)?.AddDamage(attacker, (long)hitInfo.HealthDamage + hitInfo.ShieldDamage);
    }
}
