// <copyright file="CharacterRelocationTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Tests.AdminApi;

using Moq;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Offline;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Web.AdminPanel.AdminApi.Operations;
using BasicModel = MUnique.OpenMU.Persistence.BasicModel;

/// <summary>
/// Tests the moving of stuck characters by the admin API.
/// </summary>
[TestFixture]
public class CharacterRelocationTests
{
    /// <summary>
    /// Tests that the character is moved into the rectangle of the spawn gate of the map, and not to another gate.
    /// </summary>
    [Test]
    public void MovesTheCharacterIntoTheSpawnGate()
    {
        var devias = new BasicModel.GameMapDefinition { Number = 2, Name = "Devias" };
        var lorencia = new BasicModel.GameMapDefinition { Number = 0, Name = "Lorencia" };
        lorencia.ExitGates.Add(new BasicModel.ExitGate { X1 = 10, Y1 = 20, X2 = 12, Y2 = 21 });
        lorencia.ExitGates.Add(new BasicModel.ExitGate { X1 = 130, Y1 = 116, X2 = 151, Y2 = 137, IsSpawnGate = true });
        var character = new BasicModel.Character { Name = "sapee", CurrentMap = devias, PositionX = 31, PositionY = 25 };
        var random = new Random(42);

        for (var i = 0; i < 100; i++)
        {
            Assert.That(CharacterRelocation.TryMoveToSpawn(character, lorencia, random), Is.True);
            Assert.That(character.CurrentMap, Is.SameAs(lorencia));
            Assert.That(character.PositionX, Is.InRange(130, 151));
            Assert.That(character.PositionY, Is.InRange(116, 137));
        }
    }

    /// <summary>
    /// Tests that the bounds of the spawn gate are inclusive, so a gate of one tile works.
    /// </summary>
    [Test]
    public void MovesTheCharacterToASingleTileGate()
    {
        var map = new BasicModel.GameMapDefinition { Number = 0, Name = "Lorencia" };
        map.ExitGates.Add(new BasicModel.ExitGate { X1 = 140, Y1 = 125, X2 = 140, Y2 = 125, IsSpawnGate = true });
        var character = new BasicModel.Character { Name = "sapee" };

        Assert.That(CharacterRelocation.TryMoveToSpawn(character, map, new Random(1)), Is.True);
        Assert.That((character.PositionX, character.PositionY), Is.EqualTo(((byte)140, (byte)125)));
    }

    /// <summary>
    /// Tests that a map without spawn gate leaves the character where it is, and isn't offered.
    /// </summary>
    [Test]
    public void MapWithoutSpawnGateIsRefused()
    {
        var devias = new BasicModel.GameMapDefinition { Number = 2, Name = "Devias" };
        var arena = new BasicModel.GameMapDefinition { Number = 6, Name = "Arena" };
        var lorencia = new BasicModel.GameMapDefinition { Number = 0, Name = "Lorencia" };
        lorencia.ExitGates.Add(new BasicModel.ExitGate { X1 = 130, Y1 = 116, X2 = 151, Y2 = 137, IsSpawnGate = true });
        var configuration = new BasicModel.GameConfiguration();
        configuration.Maps.Add(arena);
        configuration.Maps.Add(lorencia);
        var character = new BasicModel.Character { Name = "sapee", CurrentMap = devias, PositionX = 31, PositionY = 25 };

        Assert.That(CharacterRelocation.TryMoveToSpawn(character, arena, new Random(1)), Is.False);
        Assert.That((character.CurrentMap, character.PositionX, character.PositionY), Is.EqualTo((devias, (byte)31, (byte)25)));
        Assert.That(CharacterRelocation.GetSpawnMaps(configuration), Is.EqualTo(new[] { lorencia }));
    }

    /// <summary>
    /// Tests that a logged in account is found, even when no game server has its player (a lost session).
    /// </summary>
    [Test]
    public async Task LoggedInAccountIsInTheGame()
    {
        var session = await CharacterRelocation.FindSessionAsync("leomineita", LoginServer(("leomineita", 1)), ServerProvider()).ConfigureAwait(false);

        Assert.That(session, Does.Contain("conectada"));
    }

    /// <summary>
    /// Tests that an account which is neither logged in nor on a game server isn't in the game.
    /// </summary>
    [Test]
    public async Task DisconnectedAccountIsNotInTheGame()
    {
        var session = await CharacterRelocation.FindSessionAsync("leomineita", LoginServer(("someoneelse", 0)), ServerProvider(GameServer())).ConfigureAwait(false);

        Assert.That(session, Is.Null);
    }

    private static ILoginServer LoginServer(params (string LoginName, byte Server)[] online)
    {
        var loginServer = new Mock<ILoginServer>();
        loginServer.Setup(s => s.GetSnapshotAsync()).Returns(
            new ValueTask<Dictionary<string, byte>>(online.ToDictionary(o => o.LoginName, o => o.Server)));
        return loginServer.Object;
    }

    private static IServerProvider ServerProvider(params IManageableServer[] servers)
    {
        var provider = new Mock<IServerProvider>();
        provider.Setup(p => p.Servers).Returns(servers.ToList());
        return provider.Object;
    }

    private static IManageableServer GameServer()
    {
        var context = new Mock<IGameServerContext>();
        context.Setup(c => c.OfflinePlayerManager).Returns(new OfflinePlayerManager());
        context.Setup(c => c.GetPlayersAsync()).Returns(new ValueTask<IList<Player>>(new List<Player>()));
        var server = new Mock<IManageableServer>();
        server.Setup(s => s.Description).Returns("Server 0");
        server.As<IGameServerContextProvider>().Setup(s => s.Context).Returns(context.Object);
        return server.Object;
    }
}
