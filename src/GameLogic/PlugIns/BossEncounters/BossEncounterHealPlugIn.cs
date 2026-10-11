// <copyright file="BossEncounterHealPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.BossEncounters;

using System.Runtime.InteropServices;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Tracks the health which the players restore to the players in the arena of a running boss encounter,
/// for the ranking of the healing and its rewards. It does nothing while no encounter runs.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.BossEncounterHealPlugIn_Name), Description = nameof(PlugInResources.BossEncounterHealPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("2C8F4A61-0D3B-4B7E-A5C9-7E1F3D9B6A24")]
public sealed class BossEncounterHealPlugIn : IHealthRestoredPlugIn
{
    /// <inheritdoc />
    public void HealthRestored(IAttackable target, Player healer, float amount)
    {
        if (target is Player healedPlayer && BossEncounterContext.FindArenaOf(healedPlayer) is { } encounter)
        {
            encounter.DamageTracker.AddHealing(healer, (long)amount);
        }
    }
}
