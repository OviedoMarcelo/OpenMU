// <copyright file="LoginAction.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlayerActions;

using System.Threading;
using MUnique.OpenMU.GameLogic.Views;
using MUnique.OpenMU.GameLogic.Views.Login;

/// <summary>
/// Action to log in a player to the game.
/// </summary>
public class LoginAction
{
    /// <summary>
    /// The maximum time a login waits for the previous session of the account to finish
    /// its disconnection (which includes its last save), before it's rejected.
    /// </summary>
    internal static readonly TimeSpan PreviousSessionTeardownTimeout = TimeSpan.FromSeconds(20);

    private static int _templateCounter;

    /// <summary>
    /// Logins the specified player.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="username">The username.</param>
    /// <param name="password">The password.</param>
    public async ValueTask LoginAsync(Player player, string username, string password)
    {
        using var loggerScope = player.Logger.BeginScope(this.GetType());

        var state = await this.AuthenticateAsync(player, username, password).ConfigureAwait(false);
        if (state is null)
        {
            return;
        }

        if (!await this.ValidateAccountStateAsync(player, state.Value).ConfigureAwait(false))
        {
            return;
        }

        var (success, account) = await this.TryEstablishSessionAsync(player, username).ConfigureAwait(false);
        if (!success || account is null)
        {
            if (player.Account is null)
            {
                // A claim of this attempt. A player which is logged in already keeps its own one.
                player.GameContext.AccountSessions.Release(username, player);
            }

            return;
        }

        try
        {
            await this.FinishLoginAsync(player, username, account).ConfigureAwait(false);
            player.Logger.LogInformation("Account [{Username}] logged in ({Session}).", username, player.SessionInfo);
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "Failed to finish login for [{Username}] ({Session}).", username, player.SessionInfo);
            await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.ConnectionError)).ConfigureAwait(false);

            // The account is set already, so the player can't stay: releasing just the login server,
            // like before, left a working session behind whose account could be logged in again.
            // The disconnection releases the login server and the account claim, after saving.
            await player.DisconnectAsync().ConfigureAwait(false);
        }
    }

    private async ValueTask<AccountState?> AuthenticateAsync(Player player, string username, string password)
    {
        try
        {
            var state = await player.PersistenceContext.AuthenticateAsync(username, password).ConfigureAwait(false);
            if (state is null)
            {
                player.Logger.LogInformation("Account not found or invalid password, username: [{Username}].", username);
                await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.InvalidPassword)).ConfigureAwait(false);
            }

            return state;
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "Login Failed.");
            await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.ConnectionError)).ConfigureAwait(false);
            return null;
        }
    }

    private async ValueTask<bool> ValidateAccountStateAsync(Player player, AccountState state)
    {
        if (state == AccountState.Banned)
        {
            await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.AccountBlocked)).ConfigureAwait(false);
            return false;
        }

        if (state == AccountState.TemporarilyBanned)
        {
            await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.TemporaryBlocked)).ConfigureAwait(false);
            return false;
        }

        return true;
    }

    private async ValueTask<(bool Success, Account? Account)> TryEstablishSessionAsync(Player player, string username)
    {
        try
        {
            await using var context = await player.PlayerState.TryBeginAdvanceToAsync(PlayerState.Authenticated).ConfigureAwait(false);
            if (!context.Allowed)
            {
                await this.HandleAlreadyConnectedAsync(player, username).ConfigureAwait(false);
                return (false, null);
            }

            if (player.GameContext is not IGameServerContext gameServerContext)
            {
                context.Allowed = false;
                return (false, null);
            }

            if (player.GameContext.OfflinePlayerManager.TryGetPlayer(username, out var offlinePlayer))
            {
                var isTemplateOffline = offlinePlayer!.IsTemplatePlayer;
                if (!isTemplateOffline && !await gameServerContext.LoginServer.TryLoginAsync(username, gameServerContext.Id).ConfigureAwait(false))
                {
                    context.Allowed = false;
                    await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.AccountAlreadyConnected)).ConfigureAwait(false);
                    await gameServerContext.EventPublisher.PlayerAlreadyLoggedInAsync(gameServerContext.Id, username).ConfigureAwait(false);
                    return (false, null);
                }

                var offlineAccount = await this.HandleOfflineSessionHandoverAsync(player, username).ConfigureAwait(false);
                if (offlineAccount is null)
                {
                    if (!isTemplateOffline)
                    {
                        await gameServerContext.LoginServer.LogOffAsync(username, gameServerContext.Id).ConfigureAwait(false);
                    }

                    context.Allowed = false;
                    return (false, null);
                }

                return (true, offlineAccount);
            }

            if (!await this.TryClaimAccountAsync(player, username).ConfigureAwait(false))
            {
                context.Allowed = false;
                return (false, null);
            }

            var loadedAccount = await player.PersistenceContext.GetAccountByLoginNameAsync(username).ConfigureAwait(false);
            if (loadedAccount is null)
            {
                player.Logger.LogError("Failed to load account {Username} after authentication.", username);
                await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.ConnectionError)).ConfigureAwait(false);
                context.Allowed = false;
                return (false, null);
            }

            if (loadedAccount.IsTemplate)
            {
                // A template account doesn't persist anything, so its players can share it.
                player.GameContext.AccountSessions.Release(username, player);
            }

            if (!loadedAccount.IsTemplate && !await gameServerContext.LoginServer.TryLoginAsync(username, gameServerContext.Id).ConfigureAwait(false))
            {
                context.Allowed = false;
                await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.AccountAlreadyConnected)).ConfigureAwait(false);
                await gameServerContext.EventPublisher.PlayerAlreadyLoggedInAsync(gameServerContext.Id, username).ConfigureAwait(false);
                return (false, null);
            }

            return (true, loadedAccount);
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "Unexpected error during login through login server.");
            await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.ConnectionError)).ConfigureAwait(false);
            return (false, null);
        }
    }

    private async ValueTask<Account?> HandleOfflineSessionHandoverAsync(Player player, string username)
    {
        player.Logger.LogInformation("Account {Username} has an active offline session. Stopping it and reloading account data.", username);

        try
        {
            await player.GameContext.OfflinePlayerManager.StopAsync(username).ConfigureAwait(false);
            if (!await this.TryClaimAccountAsync(player, username).ConfigureAwait(false))
            {
                return null;
            }

            var account = await player.PersistenceContext.GetAccountByLoginNameAsync(username).ConfigureAwait(false);
            if (account is null)
            {
                player.Logger.LogError("Failed to reload account {Username} after stopping offline session.", username);
                await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.ConnectionError)).ConfigureAwait(false);
            }

            return account;
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "Failed to reload account {Username} after stopping offline session.", username);
            await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.ConnectionError)).ConfigureAwait(false);
            return null;
        }
    }

    /// <summary>
    /// Claims the account for the player before it's loaded, so that it's never loaded while a previous
    /// session of it is still in the game or still saving, see <see cref="AccountSessionRegistry"/>.
    /// </summary>
    private async ValueTask<bool> TryClaimAccountAsync(Player player, string username)
    {
        var result = await player.GameContext.AccountSessions.ClaimAsync(username, player, PreviousSessionTeardownTimeout).ConfigureAwait(false);
        if (result is AccountClaimResult.Claimed or AccountClaimResult.Shared)
        {
            return true;
        }

        var owner = player.GameContext.AccountSessions.GetOwner(username);
        player.Logger.LogWarning(
            "Login of account [{Username}] ({Session}) rejected: {Reason}, held by {OtherSession} in state {State}.",
            username,
            player.SessionInfo,
            result == AccountClaimResult.InUse ? "the account is still in the game" : "the previous session didn't finish disconnecting in time",
            owner?.SessionInfo,
            owner?.PlayerState.CurrentState);
        await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.AccountAlreadyConnected)).ConfigureAwait(false);
        return false;
    }

    private async ValueTask HandleAlreadyConnectedAsync(Player player, string username)
    {
        var result = player.LoginResultOverride ?? LoginResult.AccountAlreadyConnected;
        player.LoginResultOverride = null;
        await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(result)).ConfigureAwait(false);
        if (player.GameContext is IGameServerContext gameServerContext)
        {
            await gameServerContext.EventPublisher.PlayerAlreadyLoggedInAsync(gameServerContext.Id, username).ConfigureAwait(false);
        }
    }

    private async ValueTask FinishLoginAsync(Player player, string username, Account account)
    {
        await player.SetAccountAsync(account).ConfigureAwait(false);
        player.Logger.LogDebug("Login successful, username: [{Username}].", username);

        if (player.IsTemplatePlayer)
        {
            foreach (var character in account.Characters)
            {
                var counter = Interlocked.Increment(ref _templateCounter);
                character.Name = $"_{counter}";
            }
        }

        await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(p => p.ShowLoginResultAsync(LoginResult.Ok)).ConfigureAwait(false);

        // Once per session, so that the client shows and checks the skill requirements of this server.
        await player.InvokeViewPlugInAsync<ISkillRequirementsViewPlugIn>(p => p.ShowSkillRequirementsAsync()).ConfigureAwait(false);

        // Once per session, so that the client shows the level of the monsters of this server.
        await player.InvokeViewPlugInAsync<IMonsterLevelsViewPlugIn>(p => p.ShowMonsterLevelsAsync()).ConfigureAwait(false);
    }
}