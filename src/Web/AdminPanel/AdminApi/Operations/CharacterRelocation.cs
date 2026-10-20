// <copyright file="CharacterRelocation.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AdminApi.Operations;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.Interfaces;

/// <summary>
/// Moves a character which is stuck to the spawn point of a map, while its account isn't in the game.
/// </summary>
/// <remarks>
/// Like <c>web.unstick_character</c> of the website, but for the map which the operator chooses.
/// </remarks>
public static class CharacterRelocation
{
    /// <summary>
    /// Gets the maps which have a spawn gate, so a character can be moved to them.
    /// </summary>
    /// <param name="configuration">The game configuration.</param>
    /// <returns>The maps, ordered by their number.</returns>
    public static IEnumerable<GameMapDefinition> GetSpawnMaps(GameConfiguration configuration)
    {
        return configuration.Maps
            .Where(map => map.ExitGates.Any(g => g.IsSpawnGate))
            .OrderBy(map => map.Number)
            .ThenBy(map => map.Discriminator);
    }

    /// <summary>
    /// Moves the character to a random point of a spawn gate of the map.
    /// </summary>
    /// <param name="character">The character.</param>
    /// <param name="map">The map.</param>
    /// <param name="random">The random number generator.</param>
    /// <returns><c>true</c>, if the map has a spawn gate and the character was moved.</returns>
    public static bool TryMoveToSpawn(Character character, GameMapDefinition map, Random random)
    {
        var gates = map.ExitGates.Where(g => g.IsSpawnGate).ToList();
        if (gates.Count == 0)
        {
            return false;
        }

        var gate = gates[random.Next(gates.Count)];
        character.CurrentMap = map;
        character.PositionX = (byte)random.Next(Math.Min(gate.X1, gate.X2), Math.Max(gate.X1, gate.X2) + 1);
        character.PositionY = (byte)random.Next(Math.Min(gate.Y1, gate.Y2), Math.Max(gate.Y1, gate.Y2) + 1);
        return true;
    }

    /// <summary>
    /// Describes where the account is in the game: logged in, with a player on a game server or
    /// with an offline leveling session. While it is, the game server holds the character and
    /// would overwrite the new position.
    /// </summary>
    /// <param name="loginName">The login name of the account.</param>
    /// <param name="loginServer">The login server.</param>
    /// <param name="serverProvider">The server provider, whose in-process game servers are searched.</param>
    /// <returns>The description, or <c>null</c> if the account isn't in the game.</returns>
    public static async ValueTask<string?> FindSessionAsync(string loginName, ILoginServer loginServer, IServerProvider serverProvider)
    {
        var online = await loginServer.GetSnapshotAsync().ConfigureAwait(false);
        if (online.TryGetValue(loginName, out var serverId))
        {
            return $"La cuenta está conectada (servidor {serverId}).";
        }

        foreach (var server in serverProvider.Servers.OfType<IGameServerContextProvider>())
        {
            var description = ((IManageableServer)server).Description;
            if (server.Context.OfflinePlayerManager.IsActive(loginName))
            {
                return $"La cuenta está en offlevel ({description}).";
            }

            var players = await server.Context.GetPlayersAsync().ConfigureAwait(false);
            if (players.Any(p => string.Equals(p.Account?.LoginName, loginName, StringComparison.OrdinalIgnoreCase)))
            {
                return $"El personaje de la cuenta sigue en el juego ({description}).";
            }
        }

        return null;
    }
}
