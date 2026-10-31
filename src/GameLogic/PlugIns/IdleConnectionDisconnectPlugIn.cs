// <copyright file="IdleConnectionDisconnectPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns;

using System.Runtime.InteropServices;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Disconnects (and saves) players whose client didn't send anything for a while.
/// </summary>
/// <remarks>
/// When the internet connection of a player breaks without being closed, the server doesn't notice it:
/// the character stays in the game, and the reconnect of the client is rejected because the account is
/// still in use. The client sends a ping every 20 seconds, so a silent connection is a dead one.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.IdleConnectionDisconnectPlugIn_Name), Description = nameof(PlugInResources.IdleConnectionDisconnectPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("3E6F5A41-8C2B-4D7E-9F10-2B4C6D8E0A13")]
public class IdleConnectionDisconnectPlugIn : IPeriodicTaskPlugIn, ISupportCustomConfiguration<IdleConnectionDisconnectPlugInConfiguration>, ISupportDefaultCustomConfiguration
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(5);

    private DateTime _nextRunUtc = DateTime.UtcNow;

    /// <inheritdoc />
    public IdleConnectionDisconnectPlugInConfiguration? Configuration { get; set; }

    /// <inheritdoc />
    public async ValueTask ExecuteTaskAsync(GameContext gameContext)
    {
        var now = DateTime.UtcNow;
        if (now < this._nextRunUtc)
        {
            return;
        }

        this._nextRunUtc = now + CheckInterval;
        var timeout = (this.Configuration ??= new IdleConnectionDisconnectPlugInConfiguration()).Timeout;
        var players = await gameContext.GetPlayersAsync().ConfigureAwait(false);
        foreach (var player in players.Where(p => IsIdle(p, now, timeout)))
        {
            player.Logger.LogInformation(
                "Disconnecting {player} ({session}): its client didn't send anything since {lastPacket:u}.",
                player,
                player.SessionInfo,
                player.LastClientPacketUtc);

            // Not awaited: a disconnection of a dead connection may take a while, and shouldn't hold up the others.
            _ = DisconnectAsync(player);
        }
    }

    /// <inheritdoc />
    public void ForceStart()
    {
        this._nextRunUtc = DateTime.UtcNow;
    }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new IdleConnectionDisconnectPlugInConfiguration();

    /// <summary>
    /// Determines whether the player has a client which didn't send anything within the timeout.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="now">The current time.</param>
    /// <param name="timeout">The timeout.</param>
    /// <returns><c>true</c>, if the player should be disconnected.</returns>
    internal static bool IsIdle(Player player, DateTime now, TimeSpan timeout)
    {
        return player.LastClientPacketUtc is { } lastPacket
               && now - lastPacket > timeout
               && player.PlayerState.CurrentState != PlayerState.Disconnected
               && player.PlayerState.CurrentState != PlayerState.Finished;
    }

    private static async Task DisconnectAsync(Player player)
    {
        try
        {
            await player.DisconnectAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "Couldn't disconnect the idle player {player}.", player);
        }
    }
}
