// <copyright file="LoginActionSessionTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Moq;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlayerActions;
using MUnique.OpenMU.GameLogic.Views;
using MUnique.OpenMU.GameLogic.Views.Login;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Tests that the <see cref="LoginAction"/> never loads an account which is still held by a previous session,
/// even when the login server released it already. Two sessions of the same account overwrite each other's progress.
/// </summary>
[TestFixture]
public class LoginActionSessionTest
{
    private const string LoginName = "rangler";
    private const string Password = "secret";

    private IGameContext _innerContext = null!;
    private IGameServerContext _gameContext = null!;

    /// <summary>
    /// Sets up a game server context with an account, and a login server which always accepts the login,
    /// like it does after its entry got released while the previous session was still alive.
    /// </summary>
    [SetUp]
    public async ValueTask SetUpAsync()
    {
        this._innerContext = GameContextTestHelper.CreateGameContext();
        using (var context = this._innerContext.PersistenceContextProvider.CreateNewPlayerContext(this._innerContext.Configuration))
        {
            var account = context.CreateNew<Account>();
            account.LoginName = LoginName;
            account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password);
            await context.SaveChangesAsync().ConfigureAwait(false);
        }

        var loginServer = new Mock<ILoginServer>();
        loginServer.Setup(s => s.TryLoginAsync(It.IsAny<string>(), It.IsAny<byte>())).ReturnsAsync(true);

        var gameContext = new Mock<IGameServerContext>();
        gameContext.Setup(c => c.Configuration).Returns(this._innerContext.Configuration);
        gameContext.Setup(c => c.PersistenceContextProvider).Returns(this._innerContext.PersistenceContextProvider);
        gameContext.Setup(c => c.LoggerFactory).Returns(this._innerContext.LoggerFactory);
        gameContext.Setup(c => c.PlugInManager).Returns(this._innerContext.PlugInManager);
        gameContext.Setup(c => c.FeaturePlugIns).Returns(this._innerContext.FeaturePlugIns);
        gameContext.Setup(c => c.OfflinePlayerManager).Returns(this._innerContext.OfflinePlayerManager);
        gameContext.Setup(c => c.AccountSessions).Returns(this._innerContext.AccountSessions);
        gameContext.Setup(c => c.LoginServer).Returns(loginServer.Object);
        gameContext.Setup(c => c.EventPublisher).Returns(new Mock<IEventPublisher>().Object);
        this._gameContext = gameContext.Object;
    }

    /// <summary>
    /// A second login (with the right password) of an account which is still in the game disconnects the
    /// previous session, e.g. one whose connection broke without being closed, and then loads the account.
    /// </summary>
    [Test]
    public async ValueTask LoginDisconnectsPreviousSessionWhichIsInGameAsync()
    {
        var first = await this.LoginAsync().ConfigureAwait(false);
        Assert.That(first.Account, Is.Not.Null);

        var second = await this.LoginAsync().ConfigureAwait(false);

        Assert.That(first.PlayerState.CurrentState, Is.EqualTo(PlayerState.Finished));
        Assert.That(second.Account, Is.Not.Null);
        Mock.Get(second.LoginResultPlugIn).Verify(p => p.ShowLoginResultAsync(LoginResult.AccountAlreadyConnected), Times.Never);
        Assert.That(this._gameContext.AccountSessions.GetOwner(LoginName), Is.SameAs(second));
    }

    /// <summary>
    /// A login while the previous session is still disconnecting (saving) waits for it, and then loads the account.
    /// </summary>
    [Test]
    public async ValueTask LoginWaitsForDisconnectingSessionAsync()
    {
        var first = await this.LoginAsync().ConfigureAwait(false);
        await first.PlayerState.TryAdvanceToAsync(PlayerState.Disconnected).ConfigureAwait(false);

        var secondLogin = this.LoginAsync().AsTask();
        await Task.Delay(300).ConfigureAwait(false);
        Assert.That(secondLogin.IsCompleted, Is.False, "The account must not be loaded before the previous session saved.");

        await first.PlayerState.TryAdvanceToAsync(PlayerState.Finished).ConfigureAwait(false);
        this._gameContext.AccountSessions.ReleaseAll(first);

        var second = await secondLogin.ConfigureAwait(false);
        Assert.That(second.Account, Is.Not.Null);
        Assert.That(this._gameContext.AccountSessions.GetOwner(LoginName), Is.SameAs(second));
    }

    /// <summary>
    /// After the previous session disconnected completely, the account can be logged in again.
    /// </summary>
    [Test]
    public async ValueTask LoginSucceedsAfterPreviousSessionDisconnectedAsync()
    {
        var first = await this.LoginAsync().ConfigureAwait(false);
        await first.DisconnectAsync().ConfigureAwait(false);

        var second = await this.LoginAsync().ConfigureAwait(false);

        Assert.That(second.Account, Is.Not.Null);
        Assert.That(this._gameContext.AccountSessions.GetOwner(LoginName), Is.SameAs(second));
    }

    /// <summary>
    /// A login with a wrong password doesn't take the account away from its session.
    /// </summary>
    [Test]
    public async ValueTask FailedLoginKeepsClaimOfSessionAsync()
    {
        var first = await this.LoginAsync().ConfigureAwait(false);

        var second = await this.LoginAsync("wrong").ConfigureAwait(false);

        Assert.That(first.PlayerState.CurrentState, Is.Not.EqualTo(PlayerState.Disconnected).And.Not.EqualTo(PlayerState.Finished), "A wrong password must not disconnect the session.");
        Assert.That(this._gameContext.AccountSessions.GetOwner(LoginName), Is.SameAs(first));

        await first.DisconnectAsync().ConfigureAwait(false);
        var third = await this.LoginAsync().ConfigureAwait(false);

        Assert.That(second.Account, Is.Null);
        Assert.That(third.Account, Is.Not.Null);
    }

    /// <summary>
    /// A repeated login packet of a logged in session doesn't release its own claim.
    /// </summary>
    [Test]
    public async ValueTask RepeatedLoginKeepsClaimAsync()
    {
        var first = await this.LoginAsync().ConfigureAwait(false);

        await new LoginAction().LoginAsync(first, LoginName, Password).ConfigureAwait(false);

        Assert.That(this._gameContext.AccountSessions.GetOwner(LoginName), Is.SameAs(first));
    }

    private async ValueTask<TestPlayer> LoginAsync(string password = Password)
    {
        var player = new TestPlayer(this._gameContext);
        await player.PlayerState.TryAdvanceToAsync(PlayerState.LoginScreen).ConfigureAwait(false);
        await new LoginAction().LoginAsync(player, LoginName, password).ConfigureAwait(false);
        return player;
    }

    private sealed class TestPlayer : Player
    {
        private readonly RecordingViewPlugInContainer _viewPlugIns = new();

        public TestPlayer(IGameContext gameContext)
            : base(gameContext)
        {
        }

        public IShowLoginResultPlugIn LoginResultPlugIn => this._viewPlugIns.GetPlugIn<IShowLoginResultPlugIn>();

        protected override ICustomPlugInContainer<IViewPlugIn> CreateViewPlugInContainer() => this._viewPlugIns;
    }

    private sealed class RecordingViewPlugInContainer : ICustomPlugInContainer<IViewPlugIn>
    {
        private readonly Dictionary<Type, IViewPlugIn> _mocks = new();

        public T GetPlugIn<T>()
            where T : class, IViewPlugIn
        {
            lock (this._mocks)
            {
                if (!this._mocks.TryGetValue(typeof(T), out var mock))
                {
                    mock = new Mock<T>().Object;
                    this._mocks.Add(typeof(T), mock);
                }

                return (T)mock;
            }
        }
    }
}
