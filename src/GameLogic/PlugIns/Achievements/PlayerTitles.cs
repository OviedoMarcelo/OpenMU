// <copyright file="PlayerTitles.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.Achievements;

using System.Runtime.CompilerServices;
using MUnique.OpenMU.GameLogic.Views;

/// <summary>
/// Holds the titles which the players in the game show below their name,
/// so that they can be sent to the players who see them.
/// </summary>
public static class PlayerTitles
{
    private static readonly ConditionalWeakTable<Player, TitleDefinition> Titles = new();

    /// <summary>
    /// Gets the title which the player shows.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The title; <c>null</c>, if the player shows none.</returns>
    public static TitleDefinition? Get(Player player)
    {
        return Titles.TryGetValue(player, out var title) ? title : null;
    }

    /// <summary>
    /// Sends the titles of the player itself and of the players it sees to its client.
    /// </summary>
    /// <remarks>
    /// The titles are also sent when a player comes into view. But right after entering the game,
    /// the client may not be ready yet, so it's called again when the client is.
    /// </remarks>
    /// <param name="player">The player.</param>
    public static async ValueTask SendVisibleTitlesAsync(Player player)
    {
        List<Player> visiblePlayers;
        using (await player.ObserverLock.ReaderLockAsync())
        {
            visiblePlayers = player.Observers.OfType<Player>().ToList();
        }

        if (!visiblePlayers.Contains(player))
        {
            visiblePlayers.Add(player);
        }

        foreach (var visiblePlayer in visiblePlayers)
        {
            if (Get(visiblePlayer) is { } title)
            {
                await player.InvokeViewPlugInAsync<IPlayerTitleViewPlugIn>(p => p.ShowTitleAsync(visiblePlayer, title)).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Sets the title which the player shows.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="title">The title; <c>null</c> to show none.</param>
    internal static void Set(Player player, TitleDefinition? title)
    {
        if (title is null)
        {
            Titles.Remove(player);
        }
        else
        {
            Titles.AddOrUpdate(player, title);
        }
    }
}
