// <copyright file="AccountSessionRegistry.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic;

using System.Collections.Concurrent;
using System.Threading;

/// <summary>
/// Keeps track of which player instance of this game server owns the persistent data of an account.
/// </summary>
/// <remarks>
/// Every player has its own persistence context, which holds the account as it was loaded at login.
/// Two players of the same account at the same time overwrite each other's progress with each save,
/// and the rows deleted by one (e.g. consumed potions) are inserted again by the other one, see the
/// reconciliation in the entity framework context. The login server can't prevent that on its own:
/// it only knows whether an account is logged in, and it's released before the previous player is gone
/// in some cases (the "set offline" of the admin panel, the start of an offline session, a failed login,
/// or the late log off of a session whose entry was released already).
/// So, a player claims the account here before it loads it, and keeps the claim until its teardown,
/// including the last save, is finished.
/// </remarks>
public sealed class AccountSessionRegistry
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);

    private readonly ConcurrentDictionary<string, Player> _owners = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the player which currently owns the account, if any.
    /// </summary>
    /// <param name="loginName">The login name of the account.</param>
    /// <returns>The owning player, or <c>null</c>.</returns>
    public Player? GetOwner(string loginName) => this._owners.TryGetValue(loginName, out var owner) ? owner : null;

    /// <summary>
    /// Claims the account for the player. When the account is held by a player which is disconnecting,
    /// it waits until that player finished its teardown, so that the account is loaded with its last save.
    /// </summary>
    /// <param name="loginName">The login name of the account.</param>
    /// <param name="player">The player which wants to load the account.</param>
    /// <param name="teardownTimeout">The maximum time to wait for a disconnecting owner.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the claim.</returns>
    public async ValueTask<AccountClaimResult> ClaimAsync(string loginName, Player player, TimeSpan teardownTimeout, CancellationToken cancellationToken = default)
    {
        var deadline = DateTime.UtcNow + teardownTimeout;
        while (true)
        {
            var owner = this._owners.GetOrAdd(loginName, player);
            if (owner == player)
            {
                return AccountClaimResult.Claimed;
            }

            if (owner.IsTemplatePlayer)
            {
                return AccountClaimResult.Shared;
            }

            var ownerState = owner.PlayerState.CurrentState;
            if (ownerState == PlayerState.Finished)
            {
                // Its teardown is done, the release just didn't happen (yet).
                this.Release(loginName, owner);
                continue;
            }

            if (ownerState != PlayerState.Disconnected)
            {
                return AccountClaimResult.InUse;
            }

            if (DateTime.UtcNow >= deadline)
            {
                return AccountClaimResult.TeardownTimedOut;
            }

            await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Releases the claim of the player on the account, if it has one.
    /// </summary>
    /// <param name="loginName">The login name of the account.</param>
    /// <param name="player">The player.</param>
    /// <returns><c>true</c>, if the player had a claim on the account.</returns>
    public bool Release(string loginName, Player player)
    {
        return this._owners.TryRemove(KeyValuePair.Create(loginName, player));
    }

    /// <summary>
    /// Releases all claims of the player.
    /// </summary>
    /// <param name="player">The player.</param>
    public void ReleaseAll(Player player)
    {
        foreach (var entry in this._owners)
        {
            if (entry.Value == player)
            {
                this._owners.TryRemove(entry);
            }
        }
    }
}
