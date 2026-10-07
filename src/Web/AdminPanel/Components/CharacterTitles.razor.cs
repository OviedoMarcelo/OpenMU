// <copyright file="CharacterTitles.razor.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.Components;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlugIns.Achievements;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.Progression;
using MUnique.OpenMU.PlugIns;
using MUnique.OpenMU.Web.AdminPanel.Properties;
using MUnique.OpenMU.Web.Shared.Components.Toast;

/// <summary>
/// Shows the titles of a character on its edit page, and lets an administrator give, remove and choose them.
/// </summary>
/// <remarks>
/// When the character is in the game, the changes go through the <see cref="AchievementsPlugIn"/>, so that they
/// apply immediately and the players nearby see them. Otherwise, they're written to the storage and apply
/// when the character enters the game the next time.
/// </remarks>
public partial class CharacterTitles
{
    private const string AdminSource = "admin";

    private readonly List<TitleEntry> _unlocked = new();

    private AchievementsConfiguration? _configuration;
    private IProgressionRepository? _repository;
    private Player? _player;
    private bool _isOnline;
    private bool _isBusy;
    private string? _selectedTitleId;
    private bool _forAccount;

    /// <summary>
    /// Gets or sets the character.
    /// </summary>
    [Parameter]
    public Character Character { get; set; } = null!;

    /// <summary>
    /// Gets or sets the identifier of the account of the character.
    /// </summary>
    [Parameter]
    public Guid AccountId { get; set; }

    /// <summary>
    /// Gets or sets the game servers.
    /// </summary>
    [Inject]
    public IDictionary<int, IGameServer> GameServers { get; set; } = null!;

    /// <summary>
    /// Gets or sets the toast service.
    /// </summary>
    [Inject]
    public IToastService ToastService { get; set; } = null!;

    /// <summary>
    /// Gets or sets the logger.
    /// </summary>
    [Inject]
    public ILogger<CharacterTitles> Logger { get; set; } = null!;

    private Guid CharacterId => this.Character.GetId();

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync().ConfigureAwait(true);
        await this.LoadAsync().ConfigureAwait(true);
    }

    private IEnumerable<TitleDefinition> GetGrantableTitles()
    {
        return this._configuration?.Titles
            .Where(t => !string.IsNullOrWhiteSpace(t.Id) && this._unlocked.All(u => !string.Equals(u.Id, t.Id, StringComparison.OrdinalIgnoreCase)))
            ?? [];
    }

    private async Task LoadAsync()
    {
        var context = this.GameServers.Values.OfType<GameServer.GameServer>().FirstOrDefault()?.Context;
        var plugInId = typeof(AchievementsPlugIn).GUID;
        this._repository = ProgressionRepositoryRegistry.Current;
        this._configuration = context is not null && context.PlugInManager.IsPlugInActive(plugInId)
            ? context.Configuration.PlugInConfigurations.FirstOrDefault(c => c.TypeId == plugInId)
                ?.GetConfiguration<AchievementsConfiguration>(context.PlugInManager.CustomConfigReferenceHandler)
            : null;
        this._player = await this.FindPlayerAsync().ConfigureAwait(true);
        this._isOnline = this._player is not null;
        this._unlocked.Clear();
        if (this._configuration is null || this._repository is null)
        {
            return;
        }

        try
        {
            var unlocked = await this._repository.LoadUnlockedTitlesAsync([this.CharacterId, this.AccountId]).ConfigureAwait(true);
            var activeTitleId = (await this._repository.LoadActiveTitlesAsync([this.CharacterId]).ConfigureAwait(true)).FirstOrDefault()?.TitleId;
            foreach (var title in unlocked.DistinctBy(t => t.TitleId))
            {
                var definition = this._configuration.Titles.FirstOrDefault(t => string.Equals(t.Id, title.TitleId, StringComparison.OrdinalIgnoreCase));
                this._unlocked.Add(new TitleEntry(
                    title.TitleId,
                    definition?.Text ?? title.TitleId,
                    $"#{(definition?.GetArgb() ?? uint.MaxValue) & 0xFFFFFF:X6}",
                    title.OwnerId == this.AccountId,
                    string.Equals(title.TitleId, activeTitleId, StringComparison.OrdinalIgnoreCase)));
            }
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Couldn't load the titles of character {characterId}.", this.CharacterId);
            this.ToastService.ShowError(string.Format(Resources.UnexpectedErrorOccurred, ex.Message));
        }
    }

    private async ValueTask<Player?> FindPlayerAsync()
    {
        foreach (var server in this.GameServers.Values.OfType<GameServer.GameServer>())
        {
            var players = await server.Context.GetPlayersAsync().ConfigureAwait(true);
            if (players.FirstOrDefault(p => p.SelectedCharacter is { } character && character.GetId() == this.CharacterId) is { } player)
            {
                return player;
            }
        }

        return null;
    }

    private async Task GrantAsync()
    {
        if (string.IsNullOrEmpty(this._selectedTitleId))
        {
            return;
        }

        var titleId = this._selectedTitleId;
        await this.RunAsync(
            async () =>
            {
                if (this._player is { } player && AchievementsPlugIn.GetTrackingPlugIn(player) is { } plugIn)
                {
                    return await plugIn.GrantTitleAsync(player, titleId, AdminSource, this._forAccount).ConfigureAwait(true) is not null;
                }

                await this._repository!.AddUnlockedTitleAsync(new UnlockedTitle
                {
                    OwnerId = this._forAccount ? this.AccountId : this.CharacterId,
                    TitleId = titleId,
                    UnlockedAt = DateTime.UtcNow,
                    Source = AdminSource,
                }).ConfigureAwait(true);
                return true;
            },
            Resources.CharacterTitleGranted).ConfigureAwait(true);
        this._selectedTitleId = null;
    }

    private Task RevokeAsync(string titleId)
    {
        return this.RunAsync(
            async () =>
            {
                if (this._player is { } player && AchievementsPlugIn.GetTrackingPlugIn(player) is { } plugIn)
                {
                    return await plugIn.RevokeTitleAsync(player, titleId).ConfigureAwait(true);
                }

                var removed = await this._repository!.RemoveUnlockedTitleAsync([this.CharacterId, this.AccountId], titleId).ConfigureAwait(true);
                if (this._unlocked.Any(t => t.IsActive && t.Id == titleId))
                {
                    await this._repository.SetActiveTitleAsync(this.CharacterId, null).ConfigureAwait(true);
                }

                return removed;
            },
            Resources.CharacterTitleRemoved);
    }

    private Task SetActiveAsync(string? titleId)
    {
        return this.RunAsync(
            async () =>
            {
                if (this._player is { } player && AchievementsPlugIn.GetTrackingPlugIn(player) is { } plugIn)
                {
                    return await plugIn.SetActiveTitleAsync(player, titleId).ConfigureAwait(true) is TitleChangeResult.Changed or TitleChangeResult.Removed;
                }

                await this._repository!.SetActiveTitleAsync(this.CharacterId, titleId).ConfigureAwait(true);
                return true;
            },
            Resources.CharacterTitleActiveChanged);
    }

    private async Task RunAsync(Func<Task<bool>> action, string successMessage)
    {
        if (this._isBusy || this._repository is null)
        {
            return;
        }

        this._isBusy = true;
        try
        {
            if (await action().ConfigureAwait(true))
            {
                this.ToastService.ShowSuccess(successMessage);
            }
            else
            {
                this.ToastService.ShowError(Resources.CharacterTitleChangeFailed);
            }

            await this.LoadAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Couldn't change the titles of character {characterId}.", this.CharacterId);
            this.ToastService.ShowError(string.Format(Resources.UnexpectedErrorOccurred, ex.Message));
        }
        finally
        {
            this._isBusy = false;
        }
    }

    /// <summary>
    /// An unlocked title of the character.
    /// </summary>
    /// <param name="Id">The identifier of the title.</param>
    /// <param name="Text">The text of the title.</param>
    /// <param name="Color">The color, as "#RRGGBB".</param>
    /// <param name="IsAccount">If set to <c>true</c>, the title belongs to the account.</param>
    /// <param name="IsActive">If set to <c>true</c>, the character shows it.</param>
    private sealed record TitleEntry(string Id, string Text, string Color, bool IsAccount, bool IsActive);
}
