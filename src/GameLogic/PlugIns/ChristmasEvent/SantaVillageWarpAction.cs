// <copyright file="SantaVillageWarpAction.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChristmasEvent;

/// <summary>
/// Warps players into Santa Village (with the invitation) and back to Devias (with the Snowman).
/// </summary>
public class SantaVillageWarpAction
{
    /// <summary>
    /// Gets the gate of Santa Village where the players arrive.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The gate; <c>null</c>, if Santa Village has none.</returns>
    public async ValueTask<ExitGate?> GetSantaVillageGateAsync(Player player)
    {
        if (await player.GameContext.GetMapAsync(ChristmasEventPlugIn.SantaVillageMapNumber).ConfigureAwait(false) is { SafeZoneSpawnGate: { } gate }
            && gate.Map?.Number == ChristmasEventPlugIn.SantaVillageMapNumber)
        {
            return gate;
        }

        player.Logger.LogWarning("Santa Village has no gate where players arrive; is the update of the Christmas event installed?");
        return null;
    }

    /// <summary>
    /// Warps the player from Santa Village back to Devias, when asked at the Snowman.
    /// </summary>
    /// <param name="player">The player.</param>
    public async ValueTask WarpBackToDeviasAsync(Player player)
    {
        if (player.CurrentMap?.MapId != ChristmasEventPlugIn.SantaVillageMapNumber
            || !player.CurrentMap.GetNpcsInRange(player.Position, ChristmasEventPlugIn.SantaClausRange).Any(npc => npc.Definition.Number == ChristmasEventPlugIn.SnowmanNumber))
        {
            return;
        }

        if (await player.GameContext.GetMapAsync(ChristmasEventPlugIn.DeviasMapNumber).ConfigureAwait(false) is { SafeZoneSpawnGate: { } gate })
        {
            await player.WarpToAsync(gate).ConfigureAwait(false);
        }
    }
}
