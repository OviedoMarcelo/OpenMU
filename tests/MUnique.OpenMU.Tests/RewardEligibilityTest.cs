// <copyright file="RewardEligibilityTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.Offline;
using MUnique.OpenMU.GameLogic.PlugIns;

/// <summary>
/// Tests for the <see cref="RewardEligibility"/>.
/// </summary>
[TestFixture]
public class RewardEligibilityTest
{
    /// <summary>
    /// Tests that a player who plays without the MU Helper is actively playing.
    /// </summary>
    [Test]
    public async Task PlayerWithoutMuHelperIsActiveAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);

        Assert.That(RewardEligibility.IsActivePlay(player, false), Is.True);
    }

    /// <summary>
    /// Tests that a player with a running MU Helper only counts when the helper is allowed.
    /// </summary>
    [Test]
    public async Task MuHelperCountsOnlyWhenAllowedAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.Attributes!.AddElement(new SimpleElement(1, AggregateType.AddRaw), Stats.IsMuHelperActive);

        Assert.That(RewardEligibility.IsActivePlay(player, false), Is.False);
        Assert.That(RewardEligibility.IsActivePlay(player, true), Is.True);
    }

    /// <summary>
    /// Tests that an offline leveling character never counts, even when the MU Helper is allowed.
    /// </summary>
    [Test]
    public async Task OfflinePlayerNeverCountsAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var offlinePlayer = new OfflinePlayer(player.GameContext) { Account = player.Account };

        Assert.That(RewardEligibility.IsActivePlay(offlinePlayer, true), Is.False);
    }

    /// <summary>
    /// Tests that a bot account never counts.
    /// </summary>
    [Test]
    public async Task BotNeverCountsAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.Account!.IsBot = true;

        Assert.That(RewardEligibility.IsActivePlay(player, true), Is.False);
    }
}
