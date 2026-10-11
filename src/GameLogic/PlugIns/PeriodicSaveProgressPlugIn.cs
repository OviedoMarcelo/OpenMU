// <copyright file="PeriodicSaveProgressPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns;

using System.Runtime.InteropServices;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Saves the progress of players periodically when their status is 'EnteredWorld'.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.PeriodicSaveProgressPlugIn_Name), Description = nameof(PlugInResources.PeriodicSaveProgressPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("CEBBD5BD-B0DF-4768-816D-AF8DF78888B2")]
public class PeriodicSaveProgressPlugIn : IPeriodicTaskPlugIn, ISupportCustomConfiguration<PeriodicSaveProgressPlugInConfiguration>, ISupportDefaultCustomConfiguration
{
    private DateTime _nextRunUtc = DateTime.UtcNow;

    /// <inheritdoc />
    public PeriodicSaveProgressPlugInConfiguration? Configuration { get; set; }

    /// <inheritdoc />
    public async ValueTask ExecuteTaskAsync(GameContext gameContext)
    {
        if (DateTime.UtcNow < this._nextRunUtc)
        {
            return;
        }

        var configuration = this.Configuration ??= CreateDefaultConfiguration();
        this._nextRunUtc = DateTime.UtcNow + configuration.Interval;

        var logger = gameContext.LoggerFactory.CreateLogger(this.GetType().Name);
        using var scope = logger.BeginScope(gameContext);
        logger.LogDebug("Starting periodical save...");
        try
        {
            var players = await gameContext.GetPlayersAsync().ConfigureAwait(false);
            foreach (var player in players)
            {
                using var loggerScope = player.Logger.BeginScope(
                    ("Account", player.Account?.LoginName ?? string.Empty),
                    ("Character", player.SelectedCharacter?.Name ?? string.Empty));
                try
                {
                    if (player.PlayerState.CurrentState == PlayerState.EnteredWorld)
                    {
                        await player.SaveProgressAsync().ConfigureAwait(false);
                    }
                    else
                    {
                        player.Logger.LogDebug("Skipping saving the progress periodically for player '{player}' because the state is {state}", player, player.PlayerState.CurrentState);
                    }
                }
                catch (Exception ex)
                {
                    player.Logger.LogError(ex, "Error when saving the progress periodically for player '{player}': {ex}", player, ex);
                    await DisconnectUnsavablePlayerAsync(player).ConfigureAwait(false);
                }
            }

            logger.LogDebug("Finished periodical save without errors.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error when saving player data periodically.");
        }
    }

    /// <inheritdoc />
    public void ForceStart()
    {
        this._nextRunUtc = DateTime.UtcNow;
    }

    /// <inheritdoc />
    public object CreateDefaultConfig()
    {
        return CreateDefaultConfiguration();
    }

    /// <summary>
    /// Once a save failed, the following ones of the same session fail as well, so the player
    /// would keep playing without anything being saved. Disconnecting makes the next login load
    /// the last saved state, so only the progress since the last successful save is lost.
    /// </summary>
    /// <param name="player">The player whose progress couldn't be saved.</param>
    private static async ValueTask DisconnectUnsavablePlayerAsync(Player player)
    {
        try
        {
            await player.ShowLocalizedGoldenMessageAsync(nameof(PlayerMessage.ProgressCouldNotBeSaved)).ConfigureAwait(false);
            await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.ProgressCouldNotBeSaved)).ConfigureAwait(false);

            // Give the client a moment to show the message.
            await Task.Delay(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
            await player.DisconnectAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "Couldn't disconnect player '{player}' after a failed save.", player);
        }
    }

    private static PeriodicSaveProgressPlugInConfiguration CreateDefaultConfiguration()
    {
        return new PeriodicSaveProgressPlugInConfiguration();
    }
}