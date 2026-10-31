// <copyright file="IdleConnectionDisconnectPlugInTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlugIns;

/// <summary>
/// Tests which players the <see cref="IdleConnectionDisconnectPlugIn"/> disconnects.
/// </summary>
[TestFixture]
public class IdleConnectionDisconnectPlugInTest
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(90);

    /// <summary>
    /// A player whose client didn't send anything within the timeout is idle; one which did isn't.
    /// </summary>
    /// <param name="secondsSinceLastPacket">The seconds since the last packet of the client.</param>
    /// <param name="expected">The expected result.</param>
    [TestCase(30, false)]
    [TestCase(89, false)]
    [TestCase(120, true)]
    public async ValueTask PlayerWithClientIsIdleAfterTimeoutAsync(int secondsSinceLastPacket, bool expected)
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var now = DateTime.UtcNow;
        player.LastClientPacketUtc = now - TimeSpan.FromSeconds(secondsSinceLastPacket);

        Assert.That(IdleConnectionDisconnectPlugIn.IsIdle(player, now, Timeout), Is.EqualTo(expected));
    }

    /// <summary>
    /// Players without a client (bots, offline players) never send packets, so they're never idle.
    /// </summary>
    [Test]
    public async ValueTask PlayerWithoutClientIsNeverIdleAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.LastClientPacketUtc = null;

        Assert.That(IdleConnectionDisconnectPlugIn.IsIdle(player, DateTime.UtcNow + TimeSpan.FromHours(1), Timeout), Is.False);
    }

    /// <summary>
    /// A player which is disconnecting already isn't disconnected again.
    /// </summary>
    [Test]
    public async ValueTask DisconnectingPlayerIsNotIdleAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var now = DateTime.UtcNow;
        player.LastClientPacketUtc = now - TimeSpan.FromMinutes(5);
        await player.PlayerState.TryAdvanceToAsync(PlayerState.Disconnected).ConfigureAwait(false);

        Assert.That(IdleConnectionDisconnectPlugIn.IsIdle(player, now, Timeout), Is.False);
    }
}
