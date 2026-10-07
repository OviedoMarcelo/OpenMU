// <copyright file="PrestigePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.Prestige;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.PlayerActions;
using MUnique.OpenMU.GameLogic.PlugIns.Achievements;
using MUnique.OpenMU.GameLogic.PlugIns.WeeklyQuests;
using MUnique.OpenMU.GameLogic.Views.Login;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.Progression;
using MUnique.OpenMU.PlugIns;
using Nito.AsyncEx;

/// <summary>
/// The prestige: a character with the maximum level and master level can start its master level over,
/// in exchange for prestige points, rewards, titles and a small permanent experience bonus.
/// The resets of the character are not touched.
/// </summary>
/// <remarks>
/// The master level, the master experience, the master skills and the master points start over.
/// Like a reset, the character goes back to the character selection afterwards, so that the client
/// shows the empty master skill tree.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.PrestigePlugIn_Name), Description = nameof(PlugInResources.PrestigePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("7924F689-ABD5-4F98-8552-D781AB01E8B0")]
public class PrestigePlugIn :
    IPlayerStateChangedPlugIn,
    IExperienceCalculationPlugIn,
    ISupportCustomConfiguration<PrestigeConfiguration>,
    ISupportDefaultCustomConfiguration,
    IDisabledByDefault
{
    /// <summary>
    /// The repository which is used when no other is registered, e.g. in the demo mode.
    /// </summary>
    private static readonly InMemoryProgressionRepository FallbackRepository = new();

    private static readonly ConditionalWeakTable<Player, PrestigeState> States = new();

    private readonly IProgressionRepository? _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="PrestigePlugIn"/> class,
    /// which uses the repository of the <see cref="ProgressionRepositoryRegistry"/>.
    /// </summary>
    public PrestigePlugIn()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PrestigePlugIn"/> class.
    /// </summary>
    /// <remarks>
    /// It's internal on purpose: the plugin manager must only see the parameterless constructor.
    /// </remarks>
    /// <param name="repository">The repository of the prestige.</param>
    internal PrestigePlugIn(IProgressionRepository repository)
    {
        this._repository = repository;
    }

    /// <inheritdoc />
    public PrestigeConfiguration? Configuration { get; set; }

    private IProgressionRepository Repository => this._repository ?? ProgressionRepositoryRegistry.Current ?? FallbackRepository;

    /// <inheritdoc />
    public object CreateDefaultConfig() => PrestigeConfiguration.Default;

    /// <inheritdoc />
    public async ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        if (currentState.IsDisconnectedOrFinished() || currentState == PlayerState.CharacterSelection)
        {
            States.Remove(player);
            return;
        }

        if (previousState == PlayerState.CharacterSelection && currentState == PlayerState.EnteredWorld)
        {
            await this.GetStateAsync(player).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public ValueTask CalculateExperienceAsync(Player player, ExperienceCalculationArgs args)
    {
        if (this.Configuration is { } configuration
            && States.TryGetValue(player, out var state)
            && state.Level > 0
            && configuration.GetExperienceBonusPercent(state.Level) is > 0 and var bonus)
        {
            args.Experience *= 1 + (bonus / 100);
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Gets the prestige of the character of the player.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The prestige level and points; <c>null</c>, if they couldn't be loaded.</returns>
    public async ValueTask<(int Level, int Points)?> GetPrestigeAsync(Player player)
    {
        return await this.GetStateAsync(player).ConfigureAwait(false) is { } state
            ? (state.Level, state.Points)
            : null;
    }

    /// <summary>
    /// Gets the master level which is required for a prestige.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The required master level.</returns>
    public int GetRequiredMasterLevel(Player player)
    {
        var configured = this.Configuration?.RequiredMasterLevel ?? 0;
        return configured > 0 ? configured : player.GameContext.Configuration.MaximumMasterLevel;
    }

    /// <summary>
    /// Checks whether the character of the player can do a prestige now.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="currentPrestige">The current prestige level of the character.</param>
    /// <returns><see cref="PrestigeResult.Done"/>, if it can; otherwise the reason why it can't.</returns>
    public PrestigeResult CheckRequirements(Player player, int currentPrestige)
    {
        if (this.Configuration is not { } configuration || player.Attributes is null || player.SelectedCharacter is null)
        {
            return PrestigeResult.NotAvailable;
        }

        if (configuration.MaximumPrestige > 0 && currentPrestige >= configuration.MaximumPrestige)
        {
            return PrestigeResult.MaximumReached;
        }

        if (player.Level < configuration.RequiredLevel)
        {
            return PrestigeResult.LevelTooLow;
        }

        return player.Attributes[Stats.MasterLevel] < this.GetRequiredMasterLevel(player)
            ? PrestigeResult.MasterLevelTooLow
            : PrestigeResult.Done;
    }

    /// <summary>
    /// Does the prestige of the character of the player: hands out the rewards, starts the master level over and
    /// sends the player back to the character selection.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The result.</returns>
    public async ValueTask<PrestigeResult> PrestigeAsync(Player player)
    {
        if (this.Configuration is not { } configuration
            || player.PlayerState.CurrentState != PlayerState.EnteredWorld
            || await this.GetStateAsync(player).ConfigureAwait(false) is not { } state)
        {
            return PrestigeResult.NotAvailable;
        }

        int newLevel;
        using (await state.Lock.LockAsync().ConfigureAwait(false))
        {
            if (this.CheckRequirements(player, state.Level) is var check && check != PrestigeResult.Done)
            {
                return check;
            }

            newLevel = state.Level + 1;
            var levelDefinition = configuration.Levels.FirstOrDefault(l => l.Level == newLevel);
            if (levelDefinition is { Rewards.Count: > 0 }
                && !await WeeklyQuestRewarder.TryGiveRewardsAsync(player, levelDefinition.Rewards, $"prestige-{newLevel}").ConfigureAwait(false))
            {
                return PrestigeResult.RewardsDontFit;
            }

            var prestige = new PrestigeProgress
            {
                CharacterId = state.CharacterId,
                AccountId = player.Account?.GetId(),
                Level = newLevel,
                Points = state.Points + configuration.PointsPerPrestige,
                LastPrestigeAt = DateTime.UtcNow,
            };

            try
            {
                await this.Repository.SavePrestigeAsync(prestige).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // The rewards are handed out already, but the character stays as it is, so it isn't punished.
                player.Logger.LogError(ex, "Couldn't save the prestige {level} of character {characterId}; the rewards have been handed out anyway.", newLevel, state.CharacterId);
                return PrestigeResult.NotAvailable;
            }

            state.Level = prestige.Level;
            state.Points = prestige.Points;
            await StartMasterLevelOverAsync(player).ConfigureAwait(false);
            player.Logger.LogInformation("Character {characterId} reached the prestige {level}.", state.CharacterId, newLevel);

            if (levelDefinition?.TitleId is { Length: > 0 } titleId
                && player.GameContext.PlugInManager.GetActivePlugInsOf<IExperienceCalculationPlugIn>().OfType<AchievementsPlugIn>().FirstOrDefault() is { } achievements)
            {
                await achievements.GrantTitleAsync(player, titleId, $"prestige-{newLevel}").ConfigureAwait(false);
            }
        }

        await this.AnnounceAsync(player, newLevel).ConfigureAwait(false);

        // Like after a reset: the client has to show the empty master skill tree, so the character is loaded again.
        await new LogoutAction().LogoutAsync(player, LogoutType.BackToCharacterSelection).ConfigureAwait(false);
        return PrestigeResult.Done;
    }

    private static async ValueTask StartMasterLevelOverAsync(Player player)
    {
        var character = player.SelectedCharacter!;
        player.Attributes![Stats.MasterLevel] = 0;
        character.MasterExperience = 0;

        // The master points come back with the master levels, so the spent ones are not refunded.
        character.MasterLevelUpPoints = 0;
        foreach (var masterSkill in character.LearnedSkills.Where(s => s.Skill?.MasterDefinition is not null).ToList())
        {
            character.LearnedSkills.Remove(masterSkill);
            await player.PersistenceContext.DeleteAsync(masterSkill).ConfigureAwait(false);
        }
    }

    private async ValueTask AnnounceAsync(Player player, int prestigeLevel)
    {
        var template = this.Configuration?.AnnouncementMessage.GetTranslation(player.Culture);
        if (string.IsNullOrWhiteSpace(template))
        {
            return;
        }

        string message;
        try
        {
            message = string.Format(template, player.SelectedCharacter?.Name, prestigeLevel);
        }
        catch (FormatException)
        {
            message = template;
        }

        await player.GameContext.SendGlobalMessageAsync(message, MessageType.GoldenCenter).ConfigureAwait(false);
    }

    private async ValueTask<PrestigeState?> GetStateAsync(Player player)
    {
        if (player.SelectedCharacter is not { } character)
        {
            return null;
        }

        // After going back to the character selection, the player may have chosen another character.
        if (States.TryGetValue(player, out var state))
        {
            if (state.CharacterId == character.GetId())
            {
                return state;
            }

            States.Remove(player);
        }

        try
        {
            var characterId = character.GetId();
            var stored = (await this.Repository.LoadPrestigeAsync([characterId]).ConfigureAwait(false)).FirstOrDefault();
            var loaded = new PrestigeState(characterId) { Level = stored?.Level ?? 0, Points = stored?.Points ?? 0 };
            return States.GetValue(player, _ => loaded);
        }
        catch (Exception ex)
        {
            player.Logger.LogWarning(ex, "Couldn't load the prestige of character {characterId}.", character.GetId());
            return null;
        }
    }

    /// <summary>
    /// The prestige of a player, which is kept in memory while it's in the game.
    /// </summary>
    private sealed class PrestigeState
    {
        public PrestigeState(Guid characterId)
        {
            this.CharacterId = characterId;
        }

        public Guid CharacterId { get; }

        public AsyncLock Lock { get; } = new();

        public int Level { get; set; }

        public int Points { get; set; }
    }
}
