// <copyright file="AccountSessionRegistryTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using MUnique.OpenMU.GameLogic;

/// <summary>
/// Tests for the <see cref="AccountSessionRegistry"/>.
/// </summary>
[TestFixture]
public class AccountSessionRegistryTest
{
    private const string LoginName = "rangler";

    private static readonly TimeSpan ShortTimeout = TimeSpan.FromMilliseconds(300);

    /// <summary>
    /// A free account can be claimed, and a second claim of the same player succeeds as well.
    /// </summary>
    [Test]
    public async ValueTask FreeAccountIsClaimedAsync()
    {
        var registry = new AccountSessionRegistry();
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);

        Assert.That(await registry.ClaimAsync(LoginName, player, ShortTimeout).ConfigureAwait(false), Is.EqualTo(AccountClaimResult.Claimed));
        Assert.That(await registry.ClaimAsync(LoginName, player, ShortTimeout).ConfigureAwait(false), Is.EqualTo(AccountClaimResult.Claimed));
        Assert.That(registry.GetOwner(LoginName), Is.SameAs(player));
    }

    /// <summary>
    /// An account which is held by a player in the game can't be claimed by another one, regardless of the case of the login name.
    /// </summary>
    [Test]
    public async ValueTask AccountInUseIsNotClaimedAsync()
    {
        var registry = new AccountSessionRegistry();
        var owner = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var other = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        await registry.ClaimAsync(LoginName, owner, ShortTimeout).ConfigureAwait(false);

        Assert.That(await registry.ClaimAsync(LoginName.ToUpperInvariant(), other, ShortTimeout).ConfigureAwait(false), Is.EqualTo(AccountClaimResult.InUse));
        Assert.That(registry.GetOwner(LoginName), Is.SameAs(owner));
    }

    /// <summary>
    /// A claim waits for a disconnecting owner, and gets the account when the owner released it.
    /// </summary>
    [Test]
    public async ValueTask ClaimWaitsForDisconnectingOwnerAsync()
    {
        var registry = new AccountSessionRegistry();
        var owner = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var other = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        await registry.ClaimAsync(LoginName, owner, ShortTimeout).ConfigureAwait(false);
        await owner.PlayerState.TryAdvanceToAsync(PlayerState.Disconnected).ConfigureAwait(false);

        var claim = registry.ClaimAsync(LoginName, other, TimeSpan.FromSeconds(10)).AsTask();
        await Task.Delay(200).ConfigureAwait(false);
        Assert.That(claim.IsCompleted, Is.False, "The claim must wait while the owner is still saving.");

        registry.ReleaseAll(owner);

        Assert.That(await claim.ConfigureAwait(false), Is.EqualTo(AccountClaimResult.Claimed));
        Assert.That(registry.GetOwner(LoginName), Is.SameAs(other));
    }

    /// <summary>
    /// A claim gives up when a disconnecting owner doesn't finish in time.
    /// </summary>
    [Test]
    public async ValueTask ClaimTimesOutForStuckOwnerAsync()
    {
        var registry = new AccountSessionRegistry();
        var owner = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var other = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        await registry.ClaimAsync(LoginName, owner, ShortTimeout).ConfigureAwait(false);
        await owner.PlayerState.TryAdvanceToAsync(PlayerState.Disconnected).ConfigureAwait(false);

        Assert.That(await registry.ClaimAsync(LoginName, other, ShortTimeout).ConfigureAwait(false), Is.EqualTo(AccountClaimResult.TeardownTimedOut));
        Assert.That(registry.GetOwner(LoginName), Is.SameAs(owner));
    }

    /// <summary>
    /// An owner which finished its teardown doesn't block the account, even if it didn't release it.
    /// </summary>
    [Test]
    public async ValueTask FinishedOwnerDoesNotBlockAsync()
    {
        var registry = new AccountSessionRegistry();
        var owner = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var other = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        await registry.ClaimAsync(LoginName, owner, ShortTimeout).ConfigureAwait(false);
        await owner.PlayerState.TryAdvanceToAsync(PlayerState.Disconnected).ConfigureAwait(false);
        await owner.PlayerState.TryAdvanceToAsync(PlayerState.Finished).ConfigureAwait(false);

        Assert.That(await registry.ClaimAsync(LoginName, other, ShortTimeout).ConfigureAwait(false), Is.EqualTo(AccountClaimResult.Claimed));
    }

    /// <summary>
    /// The release of a player which doesn't own the account keeps the claim of the owner.
    /// </summary>
    [Test]
    public async ValueTask ReleaseOfOtherPlayerKeepsClaimAsync()
    {
        var registry = new AccountSessionRegistry();
        var owner = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var other = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        await registry.ClaimAsync(LoginName, owner, ShortTimeout).ConfigureAwait(false);

        Assert.That(registry.Release(LoginName, other), Is.False);
        registry.ReleaseAll(other);
        Assert.That(registry.GetOwner(LoginName), Is.SameAs(owner));
    }

    /// <summary>
    /// The disconnection of a player releases its claim, after it advanced to <see cref="PlayerState.Finished"/>.
    /// </summary>
    [Test]
    public async ValueTask DisconnectReleasesClaimAsync()
    {
        var owner = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var registry = owner.GameContext.AccountSessions;
        await registry.ClaimAsync(LoginName, owner, ShortTimeout).ConfigureAwait(false);

        await owner.DisconnectAsync().ConfigureAwait(false);

        Assert.That(owner.PlayerState.CurrentState, Is.EqualTo(PlayerState.Finished));
        Assert.That(registry.GetOwner(LoginName), Is.Null);
    }
}
