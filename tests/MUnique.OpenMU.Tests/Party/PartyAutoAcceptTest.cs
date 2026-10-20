// <copyright file="PartyAutoAcceptTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.MuHelper;
using MUnique.OpenMU.GameServer.RemoteView.MuHelper;

/// <summary>
/// Tests for the option of the MU Helper to accept party requests from anyone.
/// </summary>
[TestFixture]
public class PartyAutoAcceptTest
{
    /// <summary>
    /// The size of the configuration of the MU Helper which the client sends.
    /// </summary>
    private const int BlobLength = 257;

    /// <summary>
    /// The offset of the byte with the option, after the flags of the helper.
    /// </summary>
    private const int PartyFlagsOffset = 30;

    /// <summary>
    /// Tests that the option is read from its bit, and that a configuration of an older client, which sends zeros there, leaves it off.
    /// </summary>
    [Test]
    public void OptionIsReadFromItsBit()
    {
        var blob = new byte[BlobLength];
        Assert.That(MuHelperSettingsSerializer.TryDeserialize(blob)!.AutoAcceptAnyone, Is.False);

        blob[PartyFlagsOffset] = 1;
        Assert.That(MuHelperSettingsSerializer.TryDeserialize(blob)!.AutoAcceptAnyone, Is.True);
    }

    /// <summary>
    /// Tests that a player with the option joins the party of anyone who invites, right away.
    /// </summary>
    [Test]
    public async ValueTask PlayerWithOptionJoinsRightAwayAsync()
    {
        var gameContext = GameContextTestHelper.CreateGameContext();
        var receiver = await CreatePlayerAsync(gameContext, "Receiver").ConfigureAwait(false);
        var requester = await CreatePlayerAsync(gameContext, "Stranger").ConfigureAwait(false);
        receiver.MuHelperSettings = new MuHelperSettings { AutoAcceptAnyone = true };

        var accepted = await PartyRequestHandler.TryAutoAcceptPartyRequestAsync(receiver, requester).ConfigureAwait(false);

        Assert.That(accepted, Is.True);
        Assert.That(receiver.Party, Is.Not.Null);
        Assert.That(requester.Party, Is.SameAs(receiver.Party));
        Assert.That(receiver.Party!.PartyMaster, Is.SameAs(requester));
    }

    /// <summary>
    /// Tests that a player without the option is asked, as before.
    /// </summary>
    [Test]
    public async ValueTask PlayerWithoutOptionIsAskedAsync()
    {
        var gameContext = GameContextTestHelper.CreateGameContext();
        var receiver = await CreatePlayerAsync(gameContext, "Receiver").ConfigureAwait(false);
        var requester = await CreatePlayerAsync(gameContext, "Stranger").ConfigureAwait(false);
        receiver.MuHelperSettings = new MuHelperSettings();

        var accepted = await PartyRequestHandler.TryAutoAcceptPartyRequestAsync(receiver, requester).ConfigureAwait(false);

        Assert.That(accepted, Is.False);
        Assert.That(receiver.Party, Is.Null);
    }

    private static async ValueTask<Player> CreatePlayerAsync(IGameContext gameContext, string name)
    {
        var player = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        await player.PlayerState.TryAdvanceToAsync(PlayerState.EnteredWorld).ConfigureAwait(false);
        player.SelectedCharacter!.Name = name;
        player.IsAlive = true;
        return player;
    }
}
