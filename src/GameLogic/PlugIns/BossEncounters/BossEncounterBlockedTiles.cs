// <copyright file="BossEncounterBlockedTiles.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.BossEncounters;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.MiniGames;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.Pathfinding;

/// <summary>
/// The tiles which the objects of an encounter block (see <see cref="BossEncounterObjectSpawn.IsBlocking"/>),
/// so that the players can't walk through them, e.g. through a pillar. A tile is blocked while its object
/// is alive, in the terrain of the server and of the clients of the players on the map; the players who
/// come to the map later get the blocked tiles, too.
/// Only tiles which aren't blocked by the map are blocked, so that unblocking them never opens a wall.
/// </summary>
internal sealed class BossEncounterBlockedTiles
{
    private const TerrainAttributeType BlockingAttribute = TerrainAttributeType.Blocked;

    private readonly Dictionary<Monster, Point> _tiles = new();
    private readonly HashSet<Player> _informedPlayers = new();
    private GameMap? _map;

    /// <summary>
    /// Blocks the tile of the object, if the map doesn't block it already.
    /// </summary>
    /// <param name="monster">The object.</param>
    /// <param name="players">The players of the game server.</param>
    public async ValueTask BlockAsync(Monster monster, IEnumerable<Player> players)
    {
        var map = monster.CurrentMap;
        var tile = monster.Position;
        if ((map.Terrain.AttributeMap[tile.X, tile.Y] & (byte)BlockingAttribute) != 0)
        {
            return;
        }

        this._map = map;
        map.Terrain.ApplyTerrainAttribute(tile.X, tile.Y, BlockingAttribute, true);
        this._tiles[monster] = tile;
        foreach (var player in this.GetPlayersOnMap(players).Where(this._informedPlayers.Contains))
        {
            await SendAsync(player, true, [tile]).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Unblocks the tiles of the destroyed objects, and sends the blocked tiles to the players who came to the map.
    /// </summary>
    /// <param name="players">The players of the game server.</param>
    public async ValueTask UpdateAsync(IEnumerable<Player> players)
    {
        var destroyed = this._tiles.Where(entry => !entry.Key.IsAlive).Select(entry => entry.Key).ToList();
        foreach (var monster in destroyed)
        {
            await this.UnblockAsync(monster, players).ConfigureAwait(false);
        }

        var playersOnMap = this.GetPlayersOnMap(players).ToList();

        // A player who left the map gets them again when coming back, because the client loads the map again.
        this._informedPlayers.RemoveWhere(player => !playersOnMap.Contains(player));
        if (this._tiles.Count == 0)
        {
            return;
        }

        foreach (var player in playersOnMap.Where(this._informedPlayers.Add))
        {
            await SendAsync(player, true, this._tiles.Values.ToList()).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Unblocks all tiles, e.g. when the objects are removed.
    /// </summary>
    /// <param name="players">The players of the game server.</param>
    public async ValueTask ClearAsync(IEnumerable<Player> players)
    {
        foreach (var monster in this._tiles.Keys.ToList())
        {
            await this.UnblockAsync(monster, players).ConfigureAwait(false);
        }
    }

    private static ValueTask SendAsync(Player player, bool isBlocked, IReadOnlyCollection<Point> tiles)
    {
        var areas = tiles.Select(tile => (tile.X, tile.Y, tile.X, tile.Y)).ToList();
        return player.InvokeViewPlugInAsync<IChangeTerrainAttributesViewPlugin>(p => p.ChangeAttributesAsync(BlockingAttribute, isBlocked, areas));
    }

    private async ValueTask UnblockAsync(Monster monster, IEnumerable<Player> players)
    {
        if (!this._tiles.Remove(monster, out var tile) || this._map is not { } map)
        {
            return;
        }

        map.Terrain.ApplyTerrainAttribute(tile.X, tile.Y, BlockingAttribute, false);
        foreach (var player in this.GetPlayersOnMap(players).Where(this._informedPlayers.Contains))
        {
            await SendAsync(player, false, [tile]).ConfigureAwait(false);
        }
    }

    private IEnumerable<Player> GetPlayersOnMap(IEnumerable<Player> players)
    {
        return players.Where(player => player.CurrentMap is { } map && map == this._map);
    }
}
