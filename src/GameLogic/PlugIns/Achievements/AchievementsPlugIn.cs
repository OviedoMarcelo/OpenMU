// <copyright file="AchievementsPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.Achievements;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.MiniGames;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.PlugIns.WeeklyQuests;
using MUnique.OpenMU.GameLogic.Views;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.Progression;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Tracks the achievements of the characters and accounts, hands out their rewards and titles,
/// and shows the chosen title below the name of the characters.
/// The achievements and titles are configured in the custom configuration of this plugin.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.AchievementsPlugIn_Name), Description = nameof(PlugInResources.AchievementsPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("1E8CD88B-455D-4E49-A3B1-7199CB4CE480")]
public class AchievementsPlugIn :
    IPlayerStateChangedPlugIn,
    IAttackableGotKilledPlugIn,
    ICharacterLevelUpPlugIn,
    ICharacterMasterLevelUpPlugIn,
    ICharacterResetPlugIn,
    IMiniGameEndedPlugIn,
    IItemPickedUpPlugIn,
    IItemConsumedPlugIn,
    IItemCraftedPlugIn,
    IDuelWonPlugIn,
    IPeriodicTaskPlugIn,
    ISupportCustomConfiguration<AchievementsConfiguration>,
    ISupportDefaultCustomConfiguration
{
    /// <summary>
    /// The interval in which the progress of the players is saved.
    /// Completed achievements and players who leave the game are saved immediately.
    /// </summary>
    private static readonly TimeSpan SaveInterval = TimeSpan.FromSeconds(60);

    /// <summary>
    /// The repository which is used when no other is registered, e.g. in the demo mode.
    /// It's shared, so that all game servers of the process see the same progress.
    /// </summary>
    private static readonly InMemoryProgressionRepository FallbackRepository = new();

    private static readonly ConditionalWeakTable<Player, AchievementPlayerState> States = new();

    private readonly IProgressionRepository? _repository;

    private DateTime _nextSaveUtc = DateTime.UtcNow + SaveInterval;

    /// <summary>
    /// Initializes a new instance of the <see cref="AchievementsPlugIn"/> class,
    /// which uses the repository of the <see cref="ProgressionRepositoryRegistry"/>.
    /// </summary>
    public AchievementsPlugIn()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AchievementsPlugIn"/> class.
    /// </summary>
    /// <remarks>
    /// It's internal on purpose: the plugin manager must only see the parameterless constructor.
    /// </remarks>
    /// <param name="repository">The repository of the progress.</param>
    internal AchievementsPlugIn(IProgressionRepository repository)
    {
        this._repository = repository;
    }

    /// <inheritdoc />
    public AchievementsConfiguration? Configuration { get; set; }

    /// <summary>
    /// Gets the repository. It's resolved on each use, because the host may set it after the plugin has been created.
    /// </summary>
    private IProgressionRepository Repository => this._repository ?? ProgressionRepositoryRegistry.Current ?? FallbackRepository;

    /// <summary>
    /// Gets the plugin which tracks the achievements of the player.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The plugin, if the player is tracked by one.</returns>
    public static AchievementsPlugIn? GetTrackingPlugIn(Player player)
    {
        return States.TryGetValue(player, out var state) ? state.Owner : null;
    }

    /// <summary>
    /// Creates the achievements which a character can see from its stored progress, e.g. for the website.
    /// Hidden achievements are only included when they're completed.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="storedProgress">The stored progress of the character and its account.</param>
    /// <param name="characterId">The identifier of the character.</param>
    /// <param name="accountId">The identifier of the account of the character.</param>
    /// <returns>The achievements with the progress of the character.</returns>
    public static IReadOnlyList<AchievementOverviewEntry> CreateOverview(AchievementsConfiguration configuration, IEnumerable<AchievementProgress> storedProgress, Guid characterId, Guid? accountId)
    {
        return CreateOverview(configuration, SelectProgress(configuration, storedProgress, characterId, accountId));
    }

    /// <inheritdoc />
    public object CreateDefaultConfig() => AchievementsConfiguration.Default;

    /// <inheritdoc />
    public async ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        try
        {
            if (currentState.IsDisconnectedOrFinished() || currentState == PlayerState.CharacterSelection)
            {
                PlayerTitles.Set(player, null);
                if (States.TryGetValue(player, out var leavingState))
                {
                    States.Remove(player);
                    using var l = await leavingState.Lock.LockAsync().ConfigureAwait(false);
                    await this.SaveAsync(player, leavingState).ConfigureAwait(false);
                }

                return;
            }

            if (previousState != PlayerState.CharacterSelection || currentState != PlayerState.EnteredWorld)
            {
                return;
            }

            if (this.GetOrCreateState(player) is not { } state)
            {
                return;
            }

            using (await state.Lock.LockAsync().ConfigureAwait(false))
            {
                if (!await this.EnsureLoadedAsync(player, state).ConfigureAwait(false))
                {
                    return;
                }

                await this.RewardPendingAsync(player, state, true).ConfigureAwait(false);
                await this.UnlockMissingTitlesAsync(player, state).ConfigureAwait(false);
            }

            // Characters which reached a level before the achievement existed get it now.
            await this.UpdateCharacterValuesAsync(player).ConfigureAwait(false);

            // The players nearby got the character before its title was loaded.
            await this.ShowActiveTitleAsync(player, state).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "Unexpected error handling the achievements at a player state change.");
        }
    }

    /// <inheritdoc />
    public async ValueTask AttackableGotKilledAsync(IAttackable killed, IAttacker? killer)
    {
        var player = killer as Player ?? (killer as Monster)?.SummonedBy;
        if (player is null)
        {
            return;
        }

        if (killed is Monster monster)
        {
            if (monster.SummonedBy is not null)
            {
                return;
            }

            var mapNumber = monster.CurrentMap?.Definition.Number;
            var monsterNumber = monster.Definition.Number;
            foreach (var receiver in await this.GetKillReceiversAsync(player).ConfigureAwait(false))
            {
                await this.AddProgressAsync(
                    receiver,
                    AchievementObjectiveType.KillMonsters,
                    1,
                    a => a.IsOnMap(mapNumber) && (a.Monster is null || a.Monster.Number == monsterNumber)).ConfigureAwait(false);
            }
        }
        else if (killed is Player victim && victim != player && !IsSameIp(player, victim))
        {
            var mapNumber = player.CurrentMap?.Definition.Number;
            await this.AddProgressAsync(
                player,
                AchievementObjectiveType.KillPlayers,
                1,
                a => a.IsOnMap(mapNumber) && victim.Level >= a.MinimumVictimLevel).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public void CharacterLeveledUp(Player player)
    {
        if (!this.HasAchievements(AchievementObjectiveType.ReachLevel))
        {
            return;
        }

        // This plugin point is synchronous, so we handle it in the background.
        _ = Task.Run(async () =>
        {
            try
            {
                await this.AddProgressAsync(player, AchievementObjectiveType.ReachLevel, player.Level).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                player.Logger.LogError(ex, "Unexpected error when updating the achievements at a level up.");
            }
        });
    }

    /// <inheritdoc />
    public ValueTask CharacterMasterLeveledUpAsync(Player player)
    {
        return this.AddProgressAsync(player, AchievementObjectiveType.ReachMasterLevel, GetMasterLevel(player));
    }

    /// <inheritdoc />
    public ValueTask CharacterResetAsync(Player player, int resetCount)
    {
        return this.AddProgressAsync(player, AchievementObjectiveType.ReachResets, resetCount);
    }

    /// <inheritdoc />
    public async ValueTask MiniGameEndedAsync(MiniGameContext miniGame, ICollection<Player> finishers)
    {
        var type = miniGame.Definition.Type;
        foreach (var player in finishers)
        {
            await this.AddProgressAsync(
                player,
                AchievementObjectiveType.CompleteMiniGames,
                1,
                a => a.MiniGameType == MiniGameType.Undefined || a.MiniGameType == type).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public ValueTask ItemPickedUpAsync(Player player, Item item, bool fromPlayerInventory)
    {
        // Items which were dropped by a player don't count, otherwise an item could be handed over to another character.
        if (fromPlayerInventory)
        {
            return ValueTask.CompletedTask;
        }

        return this.ItemLevelReachedAsync(player, item);
    }

    /// <inheritdoc />
    public void ItemConsumed(Player player, Item item, Item? targetItem)
    {
        if (targetItem is null || !this.HasAchievements(AchievementObjectiveType.ObtainItemLevel))
        {
            return;
        }

        // This plugin point is synchronous, so we handle it in the background.
        _ = Task.Run(async () =>
        {
            try
            {
                await this.ItemLevelReachedAsync(player, targetItem).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                player.Logger.LogError(ex, "Unexpected error when updating the achievements after an item upgrade.");
            }
        });
    }

    /// <inheritdoc />
    public async ValueTask ItemCraftedAsync(Player player, bool success, Item? resultItem)
    {
        if (!success)
        {
            return;
        }

        await this.AddProgressAsync(player, AchievementObjectiveType.SuccessfulCraftings, 1).ConfigureAwait(false);
        if (resultItem is not null)
        {
            await this.ItemLevelReachedAsync(player, resultItem).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public ValueTask DuelWonAsync(Player winner, Player loser)
    {
        if (IsSameIp(winner, loser))
        {
            return ValueTask.CompletedTask;
        }

        return this.AddProgressAsync(winner, AchievementObjectiveType.WinDuels, 1);
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
                player.Logger.LogError(ex, "Unexpected error when saving the achievements periodically.");
            }
        }
    }

    /// <inheritdoc />
    public void ForceStart()
    {
        this._nextSaveUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets the achievements which the player can see, with its progress.
    /// Hidden achievements are only included when they're completed.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The achievements; <c>null</c>, if the progress is not available.</returns>
    public async ValueTask<IReadOnlyList<AchievementOverviewEntry>?> GetOverviewAsync(Player player)
    {
        if (this.Configuration is not { } configuration || this.GetOrCreateState(player) is not { } state)
        {
            return null;
        }

        using var l = await state.Lock.LockAsync().ConfigureAwait(false);
        if (!await this.EnsureLoadedAsync(player, state).ConfigureAwait(false))
        {
            return null;
        }

        // The player may have freed some inventory space in the meantime.
        await this.RewardPendingAsync(player, state, false).ConfigureAwait(false);
        return CreateOverview(configuration, state.Progress);
    }

    /// <summary>
    /// Gets the titles which the player has unlocked.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The unlocked titles and the active one; <c>null</c>, if they're not available.</returns>
    public async ValueTask<(IReadOnlyList<TitleDefinition> Unlocked, TitleDefinition? Active)?> GetTitlesAsync(Player player)
    {
        if (this.Configuration is not { } configuration || this.GetOrCreateState(player) is not { } state)
        {
            return null;
        }

        using var l = await state.Lock.LockAsync().ConfigureAwait(false);
        if (!await this.EnsureLoadedAsync(player, state).ConfigureAwait(false))
        {
            return null;
        }

        await this.UnlockMissingTitlesAsync(player, state).ConfigureAwait(false);
        var unlocked = configuration.Titles.Where(t => state.UnlockedTitleIds.Contains(t.Id)).ToList();
        return (unlocked, FindTitle(configuration, state.ActiveTitleId));
    }

    /// <summary>
    /// Changes the title which the character of the player shows below its name.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="titleId">The id of the title; <c>null</c> to show none.</param>
    /// <returns>The result.</returns>
    public async ValueTask<TitleChangeResult> SetActiveTitleAsync(Player player, string? titleId)
    {
        if (this.Configuration is not { } configuration || this.GetOrCreateState(player) is not { } state)
        {
            return TitleChangeResult.NotAvailable;
        }

        TitleDefinition? title = null;
        if (titleId is not null)
        {
            title = FindTitle(configuration, titleId);
            if (title is null)
            {
                return TitleChangeResult.Unknown;
            }
        }

        using (await state.Lock.LockAsync().ConfigureAwait(false))
        {
            if (!await this.EnsureLoadedAsync(player, state).ConfigureAwait(false))
            {
                return TitleChangeResult.NotAvailable;
            }

            if (title is not null && !state.UnlockedTitleIds.Contains(title.Id))
            {
                return TitleChangeResult.NotUnlocked;
            }

            try
            {
                await this.Repository.SetActiveTitleAsync(state.CharacterId, title?.Id).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                player.Logger.LogError(ex, "Couldn't save the active title of character {characterId}.", state.CharacterId);
                return TitleChangeResult.NotAvailable;
            }

            state.ActiveTitleId = title?.Id;
        }

        await this.ShowActiveTitleAsync(player, state).ConfigureAwait(false);
        return title is null ? TitleChangeResult.Removed : TitleChangeResult.Changed;
    }

    /// <summary>
    /// Unlocks a title for the character of the player, e.g. as a prize of an event.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="titleId">The id of the title.</param>
    /// <param name="source">Where the title came from, e.g. "gm".</param>
    /// <returns>The title, if it exists and could be unlocked.</returns>
    public async ValueTask<TitleDefinition?> GrantTitleAsync(Player player, string titleId, string source)
    {
        if (this.Configuration is not { } configuration
            || FindTitle(configuration, titleId) is not { } title
            || this.GetOrCreateState(player) is not { } state)
        {
            return null;
        }

        using var l = await state.Lock.LockAsync().ConfigureAwait(false);
        if (!await this.EnsureLoadedAsync(player, state).ConfigureAwait(false))
        {
            return null;
        }

        return await this.UnlockTitleAsync(player, state, title, state.CharacterId, source).ConfigureAwait(false) ? title : null;
    }

    /// <summary>
    /// Gets the players for whom a monster kill of the killer counts.
    /// </summary>
    /// <param name="killer">The killer.</param>
    /// <returns>The killer and, if configured, its party members nearby.</returns>
    internal async ValueTask<IReadOnlyList<Player>> GetKillReceiversAsync(Player killer)
    {
        if (this.Configuration is not { ShareKillsWithParty: true } || killer.Party is not { } party)
        {
            return [killer];
        }

        // Like the experience: the party members who see the killer, i.e. who are nearby on the same map.
        using (await killer.ObserverLock.ReaderLockAsync())
        {
            return party.PartyList
                .OfType<Player>()
                .Where(p => p == killer || (p.IsAlive && killer.Observers.Contains(p)))
                .ToList();
        }
    }

    /// <summary>
    /// Selects the stored progress entries which belong to a character, i.e. the ones of the character
    /// and, for achievements of the account, the ones of its account.
    /// </summary>
    /// <remarks>
    /// When the scope of an achievement was changed in the configuration, there may be entries of both owners.
    /// Only the one of the configured scope counts.
    /// </remarks>
    private static Dictionary<string, AchievementProgress> SelectProgress(AchievementsConfiguration configuration, IEnumerable<AchievementProgress> storedProgress, Guid characterId, Guid? accountId)
    {
        var scopes = configuration.Achievements
            .Where(a => !string.IsNullOrWhiteSpace(a.Id))
            .DistinctBy(a => a.Id)
            .ToDictionary(a => a.Id, a => a.Scope);
        var result = new Dictionary<string, AchievementProgress>();
        foreach (var progress in storedProgress)
        {
            var ownerId = scopes.GetValueOrDefault(progress.AchievementId) == AchievementScope.Account && accountId is { } id ? id : characterId;
            if (progress.OwnerId == ownerId)
            {
                result[progress.AchievementId] = progress;
            }
        }

        return result;
    }

    private static IReadOnlyList<AchievementOverviewEntry> CreateOverview(AchievementsConfiguration configuration, IReadOnlyDictionary<string, AchievementProgress> progressById)
    {
        var result = new List<AchievementOverviewEntry>();
        foreach (var achievement in configuration.Achievements.Where(a => !string.IsNullOrWhiteSpace(a.Id)).DistinctBy(a => a.Id))
        {
            progressById.TryGetValue(achievement.Id, out var progress);
            var isCompleted = progress?.CompletedAt is not null;
            if ((achievement.IsHidden || !achievement.IsActive) && !isCompleted)
            {
                continue;
            }

            var required = achievement.GetRequiredCount();
            result.Add(new AchievementOverviewEntry(achievement, Math.Min(progress?.Count ?? 0, required), required, isCompleted, progress?.RewardedAt is not null));
        }

        return result;
    }

    private static TitleDefinition? FindTitle(AchievementsConfiguration configuration, string? titleId)
    {
        if (string.IsNullOrWhiteSpace(titleId))
        {
            return null;
        }

        return configuration.Titles.FirstOrDefault(t => string.Equals(t.Id, titleId, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsSameIp(Player player, Player other)
    {
        return (player as IHasIpAddress)?.IpAddress is { Length: > 0 } ip
               && ip == (other as IHasIpAddress)?.IpAddress;
    }

    private static int GetMasterLevel(Player player) => (int)(player.Attributes?[Stats.MasterLevel] ?? 0);

    private static string Format(LocalizedString template, Player player, params object?[] args)
    {
        var text = template.GetTranslation(player.Culture) ?? string.Empty;
        try
        {
            return string.Format(text, args);
        }
        catch (FormatException)
        {
            // A misconfigured message shouldn't break the achievement.
            return text;
        }
    }

    private static ValueTask ShowGoldenMessageAsync(Player player, string message)
    {
        return player.InvokeViewPlugInAsync<IShowMessagePlugIn>(p => p.ShowMessageAsync(message, MessageType.GoldenCenter));
    }

    private bool HasAchievements(AchievementObjectiveType type)
    {
        return this.Configuration?.Achievements.Any(a => a.IsActive && a.ObjectiveType == type) is true;
    }

    private ValueTask ItemLevelReachedAsync(Player player, Item item)
    {
        var level = item.Level;
        return this.AddProgressAsync(player, AchievementObjectiveType.ObtainItemLevel, 1, a => level >= a.MinimumItemLevel);
    }

    /// <summary>
    /// Updates the achievements which depend on the current values of the character, like its level.
    /// </summary>
    private async ValueTask UpdateCharacterValuesAsync(Player player)
    {
        await this.AddProgressAsync(player, AchievementObjectiveType.ReachLevel, player.Level).ConfigureAwait(false);
        await this.AddProgressAsync(player, AchievementObjectiveType.ReachMasterLevel, GetMasterLevel(player)).ConfigureAwait(false);
        await this.AddProgressAsync(player, AchievementObjectiveType.ReachResets, (long)(player.Attributes?[Stats.Resets] ?? 0)).ConfigureAwait(false);
    }

    private AchievementPlayerState? GetOrCreateState(Player player)
    {
        if (States.TryGetValue(player, out var state))
        {
            return state;
        }

        if (player.SelectedCharacter is not { } character
            || player.PlayerState.CurrentState.IsDisconnectedOrFinished()
            || player.PlayerState.CurrentState == PlayerState.CharacterSelection)
        {
            return null;
        }

        var accountId = player.Account?.GetId();
        return States.GetValue(player, _ => new AchievementPlayerState(this, character.GetId(), accountId == Guid.Empty ? null : accountId));
    }

    /// <summary>
    /// Adds progress to the active achievements of the specified type.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="objectiveType">The type of the objective.</param>
    /// <param name="value">The amount which is added; for achievements which <see cref="AchievementDefinition.IsAbsolute"/>, the current value.</param>
    /// <param name="filter">The filter which decides whether the event counts for an achievement.</param>
    private async ValueTask AddProgressAsync(Player player, AchievementObjectiveType objectiveType, long value, Func<AchievementDefinition, bool>? filter = null)
    {
        if (value <= 0 || this.Configuration is not { } configuration)
        {
            return;
        }

        // A quick check before the state is loaded, as most events don't concern any achievement.
        var candidates = configuration.Achievements
            .Where(a => a.IsActive && a.ObjectiveType == objectiveType && !string.IsNullOrWhiteSpace(a.Id))
            .ToList();
        if (candidates.Count == 0 || this.GetOrCreateState(player) is not { } state)
        {
            return;
        }

        try
        {
            using var l = await state.Lock.LockAsync().ConfigureAwait(false);
            if (!await this.EnsureLoadedAsync(player, state).ConfigureAwait(false))
            {
                return;
            }

            var hasCompleted = false;
            foreach (var achievement in candidates)
            {
                state.Progress.TryGetValue(achievement.Id, out var progress);
                if (progress?.CompletedAt is not null || (filter is not null && !filter(achievement)))
                {
                    continue;
                }

                var required = achievement.GetRequiredCount();
                var previousCount = progress?.Count ?? 0;
                var count = achievement.IsAbsolute()
                    ? Math.Max(previousCount, Math.Min(value, required))
                    : Math.Min(previousCount + value, required);
                if (count == previousCount)
                {
                    continue;
                }

                progress ??= state.GetOrCreateProgress(achievement);
                progress.Count = count;
                state.DirtyAchievementIds.Add(achievement.Id);
                if (count >= required)
                {
                    progress.CompletedAt = DateTime.UtcNow;
                    hasCompleted = true;
                    await this.CompleteAsync(player, state, configuration, achievement, progress).ConfigureAwait(false);
                }
            }

            // A completion is saved immediately, so that it can't get lost.
            if (hasCompleted)
            {
                await this.SaveAsync(player, state).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "Unexpected error when adding achievement progress of type {objectiveType}.", objectiveType);
        }
    }

    private async ValueTask CompleteAsync(Player player, AchievementPlayerState state, AchievementsConfiguration configuration, AchievementDefinition achievement, AchievementProgress progress)
    {
        player.Logger.LogInformation("Character {characterId} completed the achievement {achievement}.", state.CharacterId, achievement.Id);
        if (FindTitle(configuration, achievement.TitleId) is { } title)
        {
            await this.UnlockTitleAsync(player, state, title, progress.OwnerId, achievement.Id).ConfigureAwait(false);
        }
        else if (!string.IsNullOrWhiteSpace(achievement.TitleId))
        {
            player.Logger.LogWarning("The achievement {achievement} refers to the title {title}, which isn't configured.", achievement.Id, achievement.TitleId);
        }

        await this.TryRewardAsync(player, state, achievement, progress, true).ConfigureAwait(false);
    }

    /// <summary>
    /// Unlocks the titles of completed achievements which the player doesn't have yet,
    /// e.g. because the title was assigned to the achievement or configured after it had been completed.
    /// </summary>
    private async ValueTask UnlockMissingTitlesAsync(Player player, AchievementPlayerState state)
    {
        if (this.Configuration is not { } configuration)
        {
            return;
        }

        foreach (var progress in state.Progress.Values.Where(p => p.CompletedAt is not null).ToList())
        {
            if (configuration.Achievements.FirstOrDefault(a => a.Id == progress.AchievementId) is { } achievement
                && FindTitle(configuration, achievement.TitleId) is { } title
                && !state.UnlockedTitleIds.Contains(title.Id))
            {
                await this.UnlockTitleAsync(player, state, title, progress.OwnerId, achievement.Id).ConfigureAwait(false);
            }
        }
    }

    private async ValueTask<bool> UnlockTitleAsync(Player player, AchievementPlayerState state, TitleDefinition title, Guid ownerId, string source)
    {
        if (state.UnlockedTitleIds.Contains(title.Id))
        {
            return true;
        }

        try
        {
            await this.Repository.AddUnlockedTitleAsync(new UnlockedTitle { OwnerId = ownerId, TitleId = title.Id, UnlockedAt = DateTime.UtcNow, Source = source }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "Couldn't save the unlocked title {title} of character {characterId}.", title.Id, state.CharacterId);
            return false;
        }

        state.UnlockedTitleIds.Add(title.Id);
        await ShowGoldenMessageAsync(player, Format(this.Configuration!.TitleUnlockedMessage, player, title.Text, title.Id)).ConfigureAwait(false);
        return true;
    }

    private async ValueTask ShowActiveTitleAsync(Player player, AchievementPlayerState state)
    {
        var title = this.Configuration is { } configuration ? FindTitle(configuration, state.ActiveTitleId) : null;
        var previous = PlayerTitles.Get(player);
        PlayerTitles.Set(player, title);
        if (title is null && previous is null)
        {
            return;
        }

        await player.ForEachWorldObserverAsync<IPlayerTitleViewPlugIn>(p => p.ShowTitleAsync(player, title), true).ConfigureAwait(false);
    }

    private async ValueTask<bool> EnsureLoadedAsync(Player player, AchievementPlayerState state)
    {
        if (state.IsLoaded)
        {
            return true;
        }

        if (this.Configuration is not { } configuration)
        {
            return false;
        }

        try
        {
            var ownerIds = state.AccountId is { } accountId ? new[] { state.CharacterId, accountId } : new[] { state.CharacterId };
            var loaded = await this.Repository.LoadAchievementsAsync(ownerIds).ConfigureAwait(false);
            var titles = await this.Repository.LoadUnlockedTitlesAsync(ownerIds).ConfigureAwait(false);
            var activeTitle = (await this.Repository.LoadActiveTitlesAsync([state.CharacterId]).ConfigureAwait(false)).FirstOrDefault();

            state.Progress.Clear();
            foreach (var (achievementId, progress) in SelectProgress(configuration, loaded, state.CharacterId, state.AccountId))
            {
                state.Progress[achievementId] = progress;
            }

            state.UnlockedTitleIds.Clear();
            state.UnlockedTitleIds.UnionWith(titles.Select(t => t.TitleId));
            state.ActiveTitleId = activeTitle?.TitleId;
            state.DirtyAchievementIds.Clear();
            state.IsLoaded = true;
            return true;
        }
        catch (Exception ex)
        {
            player.Logger.LogWarning(ex, "Couldn't load the achievements of character {characterId}.", state.CharacterId);
            return false;
        }
    }

    private async ValueTask SaveAsync(Player player, AchievementPlayerState state)
    {
        if (state.DirtyAchievementIds.Count == 0)
        {
            return;
        }

        var entries = state.DirtyAchievementIds
            .Where(state.Progress.ContainsKey)
            .Select(id => state.Progress[id])
            .ToList();
        try
        {
            await this.Repository.SaveAchievementsAsync(entries).ConfigureAwait(false);
            state.DirtyAchievementIds.Clear();
        }
        catch (Exception ex)
        {
            // The entries stay dirty, so they're saved at the next attempt.
            player.Logger.LogError(ex, "Couldn't save the achievements of character {characterId}.", state.CharacterId);
        }
    }

    private async ValueTask RewardPendingAsync(Player player, AchievementPlayerState state, bool showPendingMessage)
    {
        if (this.Configuration is not { } configuration)
        {
            return;
        }

        var rewarded = false;
        foreach (var progress in state.Progress.Values.Where(p => p.CompletedAt is not null && p.RewardedAt is null).ToList())
        {
            if (configuration.Achievements.FirstOrDefault(a => a.Id == progress.AchievementId) is { } achievement)
            {
                rewarded |= await this.TryRewardAsync(player, state, achievement, progress, showPendingMessage).ConfigureAwait(false);
            }
        }

        if (rewarded)
        {
            await this.SaveAsync(player, state).ConfigureAwait(false);
        }
    }

    private async ValueTask<bool> TryRewardAsync(Player player, AchievementPlayerState state, AchievementDefinition achievement, AchievementProgress progress, bool showPendingMessage)
    {
        var configuration = this.Configuration!;
        if (!await WeeklyQuestRewarder.TryGiveRewardsAsync(player, achievement.Rewards, achievement.Id).ConfigureAwait(false))
        {
            if (showPendingMessage)
            {
                await player.ShowBlueMessageAsync(Format(configuration.RewardPendingMessage, player, achievement.Name)).ConfigureAwait(false);
            }

            return false;
        }

        progress.RewardedAt = DateTime.UtcNow;
        state.DirtyAchievementIds.Add(progress.AchievementId);
        await ShowGoldenMessageAsync(player, Format(configuration.CompletedMessage, player, achievement.Name)).ConfigureAwait(false);
        return true;
    }
}
