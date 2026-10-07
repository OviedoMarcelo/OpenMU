// <copyright file="SeasonPassPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.SeasonPass;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.PlugIns.WeeklyQuests;
using MUnique.OpenMU.GameLogic.Views;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.Progression;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The season pass: an account gains experience of the pass by completing quests and by playing,
/// and receives the rewards of each reached level. The premium track has additional rewards and
/// is activated per account and season, by a game master or (later) the shop.
/// It's disabled by default: a server owner activates it together with its chat commands when a season is ready.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.SeasonPassPlugIn_Name), Description = nameof(PlugInResources.SeasonPassPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("BEA692A2-FBAD-493D-9DB8-7A3D5E449450")]
public class SeasonPassPlugIn :
    IPlayerStateChangedPlugIn,
    IQuestCompletedPlugIn,
    IAttackableGotKilledPlugIn,
    IPeriodicTaskPlugIn,
    ISupportCustomConfiguration<SeasonPassConfiguration>,
    ISupportDefaultCustomConfiguration,
    IDisabledByDefault
{
    /// <summary>
    /// The interval in which the experience of the players is saved.
    /// Players who leave the game are saved immediately.
    /// </summary>
    private static readonly TimeSpan SaveInterval = TimeSpan.FromSeconds(60);

    /// <summary>
    /// The repository which is used when no other is registered, e.g. in the demo mode.
    /// </summary>
    private static readonly InMemoryProgressionRepository FallbackRepository = new();

    private static readonly ConditionalWeakTable<Player, SeasonPassPlayerState> States = new();

    private readonly IProgressionRepository? _repository;

    private DateTime _nextSaveUtc = DateTime.UtcNow + SaveInterval;

    /// <summary>
    /// Initializes a new instance of the <see cref="SeasonPassPlugIn"/> class,
    /// which uses the repository of the <see cref="ProgressionRepositoryRegistry"/>.
    /// </summary>
    public SeasonPassPlugIn()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SeasonPassPlugIn"/> class.
    /// </summary>
    /// <remarks>
    /// It's internal on purpose: the plugin manager must only see the parameterless constructor.
    /// </remarks>
    /// <param name="repository">The repository of the progress.</param>
    internal SeasonPassPlugIn(IProgressionRepository repository)
    {
        this._repository = repository;
    }

    /// <inheritdoc />
    public SeasonPassConfiguration? Configuration { get; set; }

    /// <summary>
    /// Gets or sets the function which returns the current point in time (UTC). It exists for tests.
    /// </summary>
    internal Func<DateTime> UtcNow { get; set; } = () => DateTime.UtcNow;

    private IProgressionRepository Repository => this._repository ?? ProgressionRepositoryRegistry.Current ?? FallbackRepository;

    /// <summary>
    /// Gets the plugin which tracks the season pass of the player.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The plugin, if the player is tracked by one.</returns>
    public static SeasonPassPlugIn? GetTrackingPlugIn(Player player)
    {
        return States.TryGetValue(player, out var state) ? state.Owner : null;
    }

    /// <inheritdoc />
    public object CreateDefaultConfig() => SeasonPassConfiguration.Default;

    /// <inheritdoc />
    public async ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        try
        {
            if (currentState.IsDisconnectedOrFinished() || currentState == PlayerState.CharacterSelection)
            {
                if (States.TryGetValue(player, out var leavingState))
                {
                    States.Remove(player);
                    using var l = await leavingState.Lock.LockAsync().ConfigureAwait(false);
                    await this.SaveAsync(player, leavingState).ConfigureAwait(false);
                }

                return;
            }

            if (previousState == PlayerState.CharacterSelection && currentState == PlayerState.EnteredWorld
                && this.GetOrCreateState(player) is { } state)
            {
                using var l = await state.Lock.LockAsync().ConfigureAwait(false);
                if (await this.EnsureLoadedAsync(player, state).ConfigureAwait(false) is { } season
                    && this.CreateOverview(player, season, state).ClaimableCount > 0)
                {
                    await player.ShowBlueMessageAsync(Format(this.Configuration!.RewardsAvailableMessage, player, season.Name)).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "Unexpected error handling the season pass at a player state change.");
        }
    }

    /// <inheritdoc />
    public ValueTask QuestCompletedAsync(Player player, WeeklyQuestDefinition quest)
    {
        if (this.Configuration is not { } configuration)
        {
            return ValueTask.CompletedTask;
        }

        var experience = quest.SeasonXp > 0
            ? quest.SeasonXp
            : quest.Period switch
            {
                QuestPeriod.Daily => configuration.DailyQuestExperience,
                QuestPeriod.Once => configuration.OnceQuestExperience,
                _ => configuration.WeeklyQuestExperience,
            };
        return this.AddExperienceAsync(player, experience);
    }

    /// <inheritdoc />
    public async ValueTask AttackableGotKilledAsync(IAttackable killed, IAttacker? killer)
    {
        if (killed is not Monster { SummonedBy: null }
            || (killer as Player ?? (killer as Monster)?.SummonedBy) is not { } player
            || this.Configuration is not { MonsterKillsPerExperience: > 0, MonsterKillsExperience: > 0 } configuration
            || !States.TryGetValue(player, out var state))
        {
            return;
        }

        bool reached;
        using (await state.Lock.LockAsync().ConfigureAwait(false))
        {
            state.KillCount++;
            reached = state.KillCount >= configuration.MonsterKillsPerExperience;
            if (reached)
            {
                state.KillCount = 0;
            }
        }

        if (reached)
        {
            await this.AddExperienceAsync(player, configuration.MonsterKillsExperience).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask ExecuteTaskAsync(GameContext gameContext)
    {
        var now = DateTime.UtcNow;
        if (now < this._nextSaveUtc)
        {
            return;
        }

        this._nextSaveUtc = now + SaveInterval;
        foreach (var player in await gameContext.GetPlayersAsync().ConfigureAwait(false))
        {
            if (!States.TryGetValue(player, out var state))
            {
                continue;
            }

            try
            {
                using var l = await state.Lock.LockAsync().ConfigureAwait(false);
                await this.SaveAsync(player, state).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                player.Logger.LogError(ex, "Unexpected error when saving the season pass periodically.");
            }
        }
    }

    /// <inheritdoc />
    public void ForceStart()
    {
        this._nextSaveUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets the season pass of the account of the player.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The season pass; <see cref="SeasonPassOverview.None"/> without a running season; <c>null</c>, if it's not available.</returns>
    public async ValueTask<SeasonPassOverview?> GetOverviewAsync(Player player)
    {
        if (this.GetOrCreateState(player) is not { } state)
        {
            return null;
        }

        using var l = await state.Lock.LockAsync().ConfigureAwait(false);
        if (this.GetRunningSeason(player) is null)
        {
            return SeasonPassOverview.None;
        }

        return await this.EnsureLoadedAsync(player, state).ConfigureAwait(false) is { } season
            ? this.CreateOverview(player, season, state)
            : null;
    }

    /// <summary>
    /// Sends the season pass to the client of the player, so that it can show it in a window.
    /// </summary>
    /// <param name="player">The player.</param>
    public async ValueTask SendAsync(Player player)
    {
        if (await this.GetOverviewAsync(player).ConfigureAwait(false) is { } overview)
        {
            await player.InvokeViewPlugInAsync<ISeasonPassViewPlugIn>(p => p.ShowSeasonPassAsync(overview)).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Hands out the rewards of all reached levels which the account didn't receive yet.
    /// It stops at the first level whose rewards don't fit into the inventory.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The number of levels and tracks whose rewards have been handed out; <c>null</c>, if the pass is not available.</returns>
    public async ValueTask<int?> ClaimAsync(Player player)
    {
        if (this.Configuration is not { } configuration || this.GetOrCreateState(player) is not { } state)
        {
            return null;
        }

        int claimed;
        using (await state.Lock.LockAsync().ConfigureAwait(false))
        {
            if (await this.EnsureLoadedAsync(player, state).ConfigureAwait(false) is not { } season)
            {
                return null;
            }

            if (!state.IsPremium)
            {
                // The premium track may have been activated somewhere else in the meantime, e.g. by the shop.
                state.IsPremium = (await this.Repository.LoadSeasonPassAsync(state.AccountId, season.Id).ConfigureAwait(false)).IsPremium;
            }

            claimed = await this.ClaimReachedLevelsAsync(player, state, configuration, season).ConfigureAwait(false);
        }

        await this.SendAsync(player).ConfigureAwait(false);
        return claimed;
    }

    /// <summary>
    /// Activates the premium track of the running season for an account.
    /// </summary>
    /// <param name="gameContext">The game context, to find the player of the account if it's in the game.</param>
    /// <param name="accountId">The identifier of the account.</param>
    /// <param name="grantedBy">Who activated it, e.g. the name of a game master.</param>
    /// <returns>The season; <c>null</c>, if no season is running or it couldn't be saved.</returns>
    public async ValueTask<(SeasonDefinition Season, bool WasActive)?> GrantPremiumAsync(IGameContext gameContext, Guid accountId, string grantedBy)
    {
        if (this.Configuration?.GetRunningSeason(this.GetServerTime(gameContext)) is not { } season)
        {
            return null;
        }

        var added = await this.Repository.AddSeasonPremiumAsync(new SeasonPremium
        {
            AccountId = accountId,
            SeasonId = season.Id,
            GrantedAt = this.UtcNow(),
            GrantedBy = grantedBy,
        }).ConfigureAwait(false);

        var player = (await gameContext.GetPlayersAsync().ConfigureAwait(false)).FirstOrDefault(p => p.Account?.GetId() == accountId);
        if (player is not null && States.TryGetValue(player, out var state))
        {
            using (await state.Lock.LockAsync().ConfigureAwait(false))
            {
                state.IsPremium |= state.SeasonId == season.Id;
            }

            if (added)
            {
                await player.ShowBlueMessageAsync(Format(this.Configuration.PremiumActivatedMessage, player, season.Name)).ConfigureAwait(false);
            }

            await this.SendAsync(player).ConfigureAwait(false);
        }

        return (season, !added);
    }

    private static string Format(LocalizedString template, Player player, params object?[] args)
    {
        var text = template.GetTranslation(player.Culture) ?? string.Empty;
        try
        {
            return string.Format(text, args);
        }
        catch (FormatException)
        {
            // A misconfigured message shouldn't break the pass.
            return text;
        }
    }

    private DateTime GetServerTime(IGameContext gameContext)
    {
        return TimeZoneInfo.ConvertTimeFromUtc(this.UtcNow(), gameContext.ServerTimeZone);
    }

    private SeasonDefinition? GetRunningSeason(Player player)
    {
        return this.Configuration?.GetRunningSeason(this.GetServerTime(player.GameContext));
    }

    private SeasonPassPlayerState? GetOrCreateState(Player player)
    {
        if (States.TryGetValue(player, out var state))
        {
            return state;
        }

        if (player.SelectedCharacter is not { } character
            || player.Account?.GetId() is not { } accountId
            || accountId == Guid.Empty
            || player.PlayerState.CurrentState.IsDisconnectedOrFinished()
            || player.PlayerState.CurrentState == PlayerState.CharacterSelection)
        {
            return null;
        }

        return States.GetValue(player, _ => new SeasonPassPlayerState(this, accountId, character.GetId()));
    }

    /// <summary>
    /// Loads the state of the running season, if it's not loaded yet. When another season started, the state of the
    /// previous one is saved first.
    /// </summary>
    /// <returns>The running season; <c>null</c>, if none is running or its state couldn't be loaded.</returns>
    private async ValueTask<SeasonDefinition?> EnsureLoadedAsync(Player player, SeasonPassPlayerState state)
    {
        if (this.GetRunningSeason(player) is not { } season)
        {
            return null;
        }

        if (state.SeasonId == season.Id)
        {
            return season;
        }

        try
        {
            await this.SaveAsync(player, state).ConfigureAwait(false);
            var loaded = await this.Repository.LoadSeasonPassAsync(state.AccountId, season.Id).ConfigureAwait(false);
            state.Experience = loaded.Experience;
            state.PendingExperience = 0;
            state.IsPremium = loaded.IsPremium;
            state.Claims.Clear();
            state.Claims.UnionWith(loaded.Claims.Select(c => (c.Level, c.IsPremium)));
            state.SeasonId = season.Id;
            return season;
        }
        catch (Exception ex)
        {
            player.Logger.LogWarning(ex, "Couldn't load the season pass of account {accountId}.", state.AccountId);
            return null;
        }
    }

    private SeasonPassOverview CreateOverview(Player player, SeasonDefinition season, SeasonPassPlayerState state)
    {
        var levels = season.Levels
            .Where(l => l.Level is >= 1 and <= SeasonDefinition.MaximumLevel)
            .DistinctBy(l => l.Level)
            .OrderBy(l => l.Level)
            .Select(l => new SeasonPassLevelEntry(l, state.Claims.Contains((l.Level, false)), state.Claims.Contains((l.Level, true))))
            .ToList();
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(season.End, DateTimeKind.Unspecified), player.GameContext.ServerTimeZone);
        return new SeasonPassOverview(season, state.Experience, season.GetLevel(state.Experience), state.IsPremium, endUtc, levels);
    }

    private async ValueTask AddExperienceAsync(Player player, long experience)
    {
        if (experience <= 0 || this.GetOrCreateState(player) is not { } state)
        {
            return;
        }

        try
        {
            SeasonDefinition? reachedLevelSeason = null;
            int reachedLevel;
            using (await state.Lock.LockAsync().ConfigureAwait(false))
            {
                if (await this.EnsureLoadedAsync(player, state).ConfigureAwait(false) is not { } season)
                {
                    return;
                }

                var previousLevel = season.GetLevel(state.Experience);
                state.Experience += experience;
                state.PendingExperience += experience;
                reachedLevel = season.GetLevel(state.Experience);
                if (reachedLevel > previousLevel)
                {
                    reachedLevelSeason = season;

                    // A new level is saved immediately, so that it can't get lost.
                    await this.SaveAsync(player, state).ConfigureAwait(false);
                }
            }

            if (reachedLevelSeason is not null)
            {
                await player.InvokeViewPlugInAsync<IShowMessagePlugIn>(p => p.ShowMessageAsync(Format(this.Configuration!.LevelUpMessage, player, reachedLevel), MessageType.GoldenCenter)).ConfigureAwait(false);
            }

            await this.SendAsync(player).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "Unexpected error when adding experience to the season pass.");
        }
    }

    private async ValueTask<int> ClaimReachedLevelsAsync(Player player, SeasonPassPlayerState state, SeasonPassConfiguration configuration, SeasonDefinition season)
    {
        var claimed = 0;
        var reachedLevel = season.GetLevel(state.Experience);
        foreach (var entry in this.CreateOverview(player, season, state).Levels.Where(l => l.Level.Level <= reachedLevel))
        {
            foreach (var isPremium in new[] { false, true })
            {
                if ((isPremium && !state.IsPremium) || state.Claims.Contains((entry.Level.Level, isPremium)))
                {
                    continue;
                }

                var result = await this.TryClaimAsync(player, state, season, entry.Level, isPremium).ConfigureAwait(false);
                if (result is null)
                {
                    await player.ShowBlueMessageAsync(Format(configuration.RewardPendingMessage, player, entry.Level.Level)).ConfigureAwait(false);
                    return claimed;
                }

                if (result is { Length: > 0 } rewards)
                {
                    claimed++;
                    await player.ShowBlueMessageAsync(Format(configuration.ClaimedMessage, player, entry.Level.Level, rewards)).ConfigureAwait(false);
                }
            }
        }

        return claimed;
    }

    /// <summary>
    /// Hands out the rewards of a level and track.
    /// </summary>
    /// <returns>The text of the handed out rewards (empty, if there were none); <c>null</c>, if they couldn't be handed out.</returns>
    private async ValueTask<string?> TryClaimAsync(Player player, SeasonPassPlayerState state, SeasonDefinition season, SeasonLevelDefinition level, bool isPremium)
    {
        var claim = new SeasonClaim
        {
            AccountId = state.AccountId,
            SeasonId = season.Id,
            Level = level.Level,
            IsPremium = isPremium,
            ClaimedAt = this.UtcNow(),
            CharacterId = state.CharacterId,
        };

        // The claim is recorded first, so that the rewards can never be handed out twice.
        if (!await this.Repository.AddSeasonClaimAsync(claim).ConfigureAwait(false))
        {
            state.Claims.Add((level.Level, isPremium));
            return string.Empty;
        }

        var rewards = isPremium ? level.PremiumRewards : level.FreeRewards;
        if (!await WeeklyQuestRewarder.TryGiveRewardsAsync(player, rewards, $"{season.Id}/{level.Level}").ConfigureAwait(false))
        {
            await this.Repository.RemoveSeasonClaimAsync(claim).ConfigureAwait(false);
            return null;
        }

        state.Claims.Add((level.Level, isPremium));
        player.Logger.LogInformation("Account {accountId} received the {track} rewards of level {level} of the season pass {season}.", state.AccountId, isPremium ? "premium" : "free", level.Level, season.Id);
        return level.GetRewardsText(isPremium, player.Culture);
    }

    private async ValueTask SaveAsync(Player player, SeasonPassPlayerState state)
    {
        if (state.PendingExperience <= 0 || state.SeasonId is not { } seasonId)
        {
            return;
        }

        try
        {
            var pending = state.PendingExperience;
            var total = await this.Repository.AddSeasonExperienceAsync(state.AccountId, seasonId, pending).ConfigureAwait(false);
            state.PendingExperience -= pending;

            // Another server may have added experience to the account at the same time.
            state.Experience = Math.Max(state.Experience, total + state.PendingExperience);
        }
        catch (Exception ex)
        {
            // The experience stays pending, so it's saved at the next attempt.
            player.Logger.LogError(ex, "Couldn't save the season pass of account {accountId}.", state.AccountId);
        }
    }
}
