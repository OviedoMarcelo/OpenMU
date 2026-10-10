// <copyright file="GameMasterPresencePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns;

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Tells all players when a game master enters the game, and when they leave it.
/// Invisible game masters (account state "GameMasterInvisible") aren't announced.
/// </summary>
[PlugIn]
[Display(Name = "Game Master Presence", Description = "Avisa a todos los jugadores cuando un GM entra al juego y cuando sale, con el nombre de su personaje. Los GM invisibles no se anuncian.")]
[Guid("3C8E5F4A-2B7D-4E61-9A0C-7D4B1E9F6A25")]
public class GameMasterPresencePlugIn : IPlayerStateChangedPlugIn, ISupportCustomConfiguration<GameMasterPresenceConfiguration>, ISupportDefaultCustomConfiguration, IDisabledByDefault
{
    /// <summary>
    /// The game masters which were announced as online, with the name of their character: they are announced
    /// as offline however they leave (logout, back to the character selection or a lost connection).
    /// </summary>
    private readonly ConcurrentDictionary<Player, string> _onlineGameMasters = new();

    /// <inheritdoc />
    public GameMasterPresenceConfiguration? Configuration { get; set; }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new GameMasterPresenceConfiguration();

    /// <inheritdoc />
    public async ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        if (this.Configuration is not { } configuration)
        {
            return;
        }

        if (currentState == PlayerState.EnteredWorld && previousState == PlayerState.CharacterSelection)
        {
            if (!IsAnnouncedGameMaster(player) || player.SelectedCharacter?.Name is not { } name)
            {
                return;
            }

            this._onlineGameMasters[player] = name;
            await AnnounceAsync(player, configuration.OnlineMessage, name, configuration.OnlineMessageType).ConfigureAwait(false);
        }
        else if ((currentState == PlayerState.Disconnected || currentState == PlayerState.Authenticated || currentState == PlayerState.CharacterSelection)
                 && this._onlineGameMasters.TryRemove(player, out var name))
        {
            await AnnounceAsync(player, configuration.OfflineMessage, name, configuration.OfflineMessageType).ConfigureAwait(false);
        }
    }

    private static bool IsAnnouncedGameMaster(Player player)
    {
        if (player is Offline.OfflinePlayer || player.Account is not { IsBot: false } account)
        {
            return false;
        }

        if (account.State == AccountState.GameMasterInvisible)
        {
            return false;
        }

        return account.State == AccountState.GameMaster || player.SelectedCharacter?.CharacterStatus == CharacterStatus.GameMaster;
    }

    private static async ValueTask AnnounceAsync(Player player, string message, string name, MessageType messageType)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        try
        {
            await player.GameContext.SendGlobalMessageAsync(string.Format(message, name), messageType).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "Couldn't announce the presence of the game master {name}.", name);
        }
    }
}
