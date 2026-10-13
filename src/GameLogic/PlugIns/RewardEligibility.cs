// <copyright file="RewardEligibility.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns;

using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.Offline;

/// <summary>
/// Decides whether an action of a player counts for the progression systems (season pass, quests, achievements).
/// Only active play counts: offline leveling and bots never do, and the MU Helper only when a system allows it.
/// </summary>
public static class RewardEligibility
{
    /// <summary>
    /// Gets the player who is credited for a kill: the killer itself, or the owner of a summoned monster.
    /// </summary>
    /// <param name="killer">The killer.</param>
    /// <returns>The credited player, if any.</returns>
    public static Player? GetCreditedPlayer(IAttacker? killer)
    {
        return killer as Player ?? (killer as Monster)?.SummonedBy;
    }

    /// <summary>
    /// Determines whether the player is actively playing, so that its actions count for the progression systems.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="countMuHelper">If set to <c>true</c>, actions while the MU Helper is running count, too.</param>
    /// <returns><c>true</c>, if the actions of the player count.</returns>
    public static bool IsActivePlay(Player player, bool countMuHelper)
    {
        // Offline leveling characters and bots are both offline players.
        if (player is OfflinePlayer || player.Account?.IsBot is true)
        {
            return false;
        }

        return countMuHelper || !(player.Attributes?[Stats.IsMuHelperActive] > 0);
    }
}
