// <copyright file="CosmeticSetsPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.CosmeticSets;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.Views;
using MUnique.OpenMU.GameLogic.Views.World;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.Progression;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Lets the characters wear cosmetic sets over their armor: the pieces of the set are shown
/// instead of the real ones, without changing the stats. The sets are unlocked by game masters
/// or as rewards, and configured in the custom configuration of this plugin.
/// </summary>
/// <remarks>
/// It's disabled by default, because it's custom gameplay which the original game didn't have.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.CosmeticSetsPlugIn_Name), Description = nameof(PlugInResources.CosmeticSetsPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("5B0E7C0A-3E54-4F0B-9C1B-7D2F1F6E8A41")]
public class CosmeticSetsPlugIn : IPlayerStateChangedPlugIn, ISupportCustomConfiguration<CosmeticSetsConfiguration>, ISupportDefaultCustomConfiguration, IDisabledByDefault
{
    /// <summary>
    /// The maximum length of the source of an unlocked set, which the storage can hold.
    /// </summary>
    private const int MaximumSourceLength = 64;

    /// <summary>
    /// The repository which is used when no other is registered, e.g. in the demo mode.
    /// </summary>
    private static readonly InMemoryProgressionRepository FallbackRepository = new();

    private static readonly ConditionalWeakTable<Player, CosmeticSetPlayerState> States = new();

    private readonly IProgressionRepository? _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="CosmeticSetsPlugIn"/> class,
    /// which uses the repository of the <see cref="ProgressionRepositoryRegistry"/>.
    /// </summary>
    public CosmeticSetsPlugIn()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CosmeticSetsPlugIn"/> class.
    /// </summary>
    /// <remarks>
    /// It's internal on purpose: the plugin manager must only see the parameterless constructor.
    /// </remarks>
    /// <param name="repository">The repository of the sets.</param>
    internal CosmeticSetsPlugIn(IProgressionRepository repository)
    {
        this._repository = repository;
    }

    /// <inheritdoc />
    public CosmeticSetsConfiguration? Configuration { get; set; }

    /// <summary>
    /// Gets the repository. It's resolved on each use, because the host may set it after the plugin has been created.
    /// </summary>
    internal IProgressionRepository Repository => this._repository ?? ProgressionRepositoryRegistry.Current ?? FallbackRepository;

    /// <summary>
    /// Gets the plugin which keeps the cosmetic sets of the player, if it's active.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The plugin; <c>null</c>, if it's not active.</returns>
    public static CosmeticSetsPlugIn? GetActivePlugIn(Player player)
    {
        return GetActivePlugIn(player.GameContext);
    }

    /// <summary>
    /// Gets the plugin of the game context, if it's active.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The plugin; <c>null</c>, if it's not active.</returns>
    public static CosmeticSetsPlugIn? GetActivePlugIn(IGameContext gameContext)
    {
        return gameContext.PlugInManager.GetActivePlugInsOf<IPlayerStateChangedPlugIn>().OfType<CosmeticSetsPlugIn>().FirstOrDefault();
    }

    /// <inheritdoc />
    public object CreateDefaultConfig() => CosmeticSetsConfiguration.Default;

    /// <inheritdoc />
    public async ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        try
        {
            if (currentState.IsDisconnectedOrFinished() || currentState == PlayerState.CharacterSelection)
            {
                PlayerCosmetics.Set(player, null);
                States.Remove(player);
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
            }

            // The players nearby got the character before its set was loaded.
            await this.ApplyActiveSetAsync(player, state, true).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "Unexpected error handling the cosmetic sets at a player state change.");
        }
    }

    /// <summary>
    /// Sends the cosmetic sets of the player to its client, e.g. when the client asks for its data again.
    /// </summary>
    /// <param name="player">The player.</param>
    public async ValueTask SendSetsAsync(Player player)
    {
        if (await this.GetSetsAsync(player).ConfigureAwait(false) is { } sets)
        {
            await player.InvokeViewPlugInAsync<IShowCosmeticSetsPlugIn>(p => p.ShowCosmeticSetsAsync(sets.Sets, sets.Active)).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Gets the configured sets, with the information whether the character of the player unlocked them.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The sets and the one the character wears; <c>null</c>, if they're not available.</returns>
    public async ValueTask<(IReadOnlyList<CosmeticSetEntry> Sets, CosmeticSetDefinition? Active)?> GetSetsAsync(Player player)
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

        var sets = configuration.Sets
            .Where(s => s.Number > 0)
            .DistinctBy(s => s.Number)
            .OrderBy(s => s.Number)
            .Select(s => new CosmeticSetEntry(s, state.UnlockedSetNumbers.Contains(s.Number), this.CanWear(player, s)))
            .ToList();
        return (sets, configuration.FindSet(state.ActiveSetNumber));
    }

    /// <summary>
    /// Changes the cosmetic set which the character of the player wears.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="setNumber">The number of the set; <c>null</c> to wear none.</param>
    /// <returns>The result.</returns>
    public async ValueTask<CosmeticSetChangeResult> SetActiveSetAsync(Player player, short? setNumber)
    {
        if (this.Configuration is not { } configuration || this.GetOrCreateState(player) is not { } state)
        {
            return CosmeticSetChangeResult.NotAvailable;
        }

        var set = configuration.FindSet(setNumber);
        if (setNumber is not null && set is null)
        {
            return CosmeticSetChangeResult.Unknown;
        }

        if (set is not null && !this.CanWear(player, set))
        {
            return CosmeticSetChangeResult.NotQualified;
        }

        using (await state.Lock.LockAsync().ConfigureAwait(false))
        {
            if (!await this.EnsureLoadedAsync(player, state).ConfigureAwait(false))
            {
                return CosmeticSetChangeResult.NotAvailable;
            }

            if (set is not null && !state.UnlockedSetNumbers.Contains(set.Number))
            {
                return CosmeticSetChangeResult.NotUnlocked;
            }

            try
            {
                await this.Repository.SetActiveCosmeticSetAsync(state.CharacterId, set?.Number).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                player.Logger.LogError(ex, "Couldn't save the active cosmetic set of character {characterId}.", state.CharacterId);
                return CosmeticSetChangeResult.NotAvailable;
            }

            state.ActiveSetNumber = set?.Number;
        }

        await this.ApplyActiveSetAsync(player, state, false).ConfigureAwait(false);
        return set is null ? CosmeticSetChangeResult.Removed : CosmeticSetChangeResult.Changed;
    }

    /// <summary>
    /// Unlocks a cosmetic set for the character of the player, e.g. as a reward.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="setNumber">The number of the set.</param>
    /// <param name="source">Where the set came from, e.g. "gm".</param>
    /// <returns>The set, if it exists and could be unlocked or was already unlocked.</returns>
    public async ValueTask<CosmeticSetDefinition?> GrantAsync(Player player, short setNumber, string source)
    {
        if (this.Configuration is not { } configuration
            || configuration.FindSet(setNumber) is not { } set
            || this.GetOrCreateState(player) is not { } state)
        {
            return null;
        }

        using (await state.Lock.LockAsync().ConfigureAwait(false))
        {
            if (!await this.EnsureLoadedAsync(player, state).ConfigureAwait(false))
            {
                return null;
            }

            if (state.UnlockedSetNumbers.Contains(set.Number))
            {
                return set;
            }

            try
            {
                await this.Repository.AddUnlockedCosmeticSetAsync(new UnlockedCosmeticSet { CharacterId = state.CharacterId, SetNumber = set.Number, UnlockedAt = DateTime.UtcNow, Source = TruncateSource(source) }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                player.Logger.LogError(ex, "Couldn't save the unlocked cosmetic set {set} of character {characterId}.", set.Number, state.CharacterId);
                return null;
            }

            state.UnlockedSetNumbers.Add(set.Number);
        }

        await player.InvokeViewPlugInAsync<IShowMessagePlugIn>(p => p.ShowMessageAsync(Format(configuration.UnlockedMessage, player, set.Name, set.Number), MessageType.GoldenCenter)).ConfigureAwait(false);
        await this.SendSetsAsync(player).ConfigureAwait(false);
        return set;
    }

    /// <summary>
    /// Removes an unlocked cosmetic set from the character of the player, e.g. when it was given by mistake.
    /// If the character wears it, it wears none afterwards.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="setNumber">The number of the set.</param>
    /// <returns><c>true</c>, if the set has been removed.</returns>
    public async ValueTask<bool> RevokeAsync(Player player, short setNumber)
    {
        if (this.GetOrCreateState(player) is not { } state)
        {
            return false;
        }

        bool removed;
        bool wasActive;
        using (await state.Lock.LockAsync().ConfigureAwait(false))
        {
            if (!await this.EnsureLoadedAsync(player, state).ConfigureAwait(false))
            {
                return false;
            }

            removed = await this.Repository.RemoveUnlockedCosmeticSetAsync(state.CharacterId, setNumber).ConfigureAwait(false);
            state.UnlockedSetNumbers.Remove(setNumber);
            wasActive = state.ActiveSetNumber == setNumber;
            if (wasActive)
            {
                await this.Repository.SetActiveCosmeticSetAsync(state.CharacterId, null).ConfigureAwait(false);
                state.ActiveSetNumber = null;
            }
        }

        if (wasActive)
        {
            await this.ApplyActiveSetAsync(player, state, false).ConfigureAwait(false);
        }
        else
        {
            await this.SendSetsAsync(player).ConfigureAwait(false);
        }

        return removed;
    }

    private static string TruncateSource(string source)
    {
        return source.Length > MaximumSourceLength ? source[..MaximumSourceLength] : source;
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
            // A misconfigured message shouldn't break the reward.
            return text;
        }
    }

    private bool CanWear(Player player, CosmeticSetDefinition set)
    {
        if (this.Configuration is not { RestrictToQualifiedClasses: true })
        {
            return true;
        }

        if (player.SelectedCharacter?.CharacterClass is not { } characterClass)
        {
            return false;
        }

        // The armor is the piece which every set has.
        var pieces = PlayerCosmetics.GetPieceDefinitions(player.GameContext.Configuration, set);
        return pieces.Count > 0 && pieces.Values.All(definition => definition.QualifiedCharacters.Contains(characterClass));
    }

    private CosmeticSetPlayerState? GetOrCreateState(Player player)
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

        return States.GetValue(player, _ => new CosmeticSetPlayerState(character.GetId()));
    }

    private async ValueTask<bool> EnsureLoadedAsync(Player player, CosmeticSetPlayerState state)
    {
        if (state.IsLoaded)
        {
            return true;
        }

        try
        {
            var unlocked = await this.Repository.LoadUnlockedCosmeticSetsAsync(state.CharacterId).ConfigureAwait(false);
            var active = await this.Repository.LoadActiveCosmeticSetAsync(state.CharacterId).ConfigureAwait(false);
            state.UnlockedSetNumbers.Clear();
            state.UnlockedSetNumbers.UnionWith(unlocked.Select(s => s.SetNumber));
            state.ActiveSetNumber = active;
            state.IsLoaded = true;
            return true;
        }
        catch (Exception ex)
        {
            player.Logger.LogWarning(ex, "Couldn't load the cosmetic sets of character {characterId}.", state.CharacterId);
            return false;
        }
    }

    /// <summary>
    /// Applies the active set to the appearance of the player, and shows it to the player and the players nearby.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="state">The state.</param>
    /// <param name="onlyIfWearing">If set to <c>true</c>, the observers are only updated when the player wears a set, e.g. after entering the game.</param>
    private async ValueTask ApplyActiveSetAsync(Player player, CosmeticSetPlayerState state, bool onlyIfWearing)
    {
        var set = this.Configuration?.FindSet(state.ActiveSetNumber);
        if (set is not null && !this.CanWear(player, set))
        {
            // The class may have changed, or the configuration.
            set = null;
        }

        var wasWearing = PlayerCosmetics.IsWearingSet(player);
        PlayerCosmetics.Set(player, set);
        await this.SendSetsAsync(player).ConfigureAwait(false);
        if (set is null && (onlyIfWearing || !wasWearing))
        {
            return;
        }

        player.OnAppearanceChanged();
        await player.ForEachWorldObserverAsync<INewPlayersInScopePlugIn>(p => p.NewPlayersInScopeAsync(player.GetAsEnumerable()), false).ConfigureAwait(false);
    }
}
