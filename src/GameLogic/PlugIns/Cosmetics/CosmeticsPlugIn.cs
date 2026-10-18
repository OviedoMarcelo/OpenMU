// <copyright file="CosmeticsPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.Cosmetics;

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.Views;
using MUnique.OpenMU.GameLogic.Views.World;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.Progression;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Lets the characters wear cosmetic pieces over their armor: a piece is shown instead of the real item
/// of its slot, without changing the stats. The pieces are unlocked one by one by game masters or as
/// rewards, and configured in the custom configuration of this plugin.
/// </summary>
/// <remarks>
/// It's disabled by default, because it's custom gameplay which the original game didn't have.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.CosmeticsPlugIn_Name), Description = nameof(PlugInResources.CosmeticsPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("5B0E7C0A-3E54-4F0B-9C1B-7D2F1F6E8A41")]
public class CosmeticsPlugIn : IPlayerStateChangedPlugIn, ISupportCustomConfiguration<CosmeticsConfiguration>, ISupportDefaultCustomConfiguration, IDisabledByDefault
{
    /// <summary>
    /// The maximum length of the source of an unlocked piece, which the storage can hold.
    /// </summary>
    private const int MaximumSourceLength = 64;

    /// <summary>
    /// The repository which is used when no other is registered, e.g. in the demo mode.
    /// </summary>
    private static readonly InMemoryProgressionRepository FallbackRepository = new();

    private static readonly ConditionalWeakTable<Player, CosmeticsPlayerState> States = new();

    private readonly IProgressionRepository? _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="CosmeticsPlugIn"/> class,
    /// which uses the repository of the <see cref="ProgressionRepositoryRegistry"/>.
    /// </summary>
    public CosmeticsPlugIn()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CosmeticsPlugIn"/> class.
    /// </summary>
    /// <remarks>
    /// It's internal on purpose: the plugin manager must only see the parameterless constructor.
    /// </remarks>
    /// <param name="repository">The repository of the pieces.</param>
    internal CosmeticsPlugIn(IProgressionRepository repository)
    {
        this._repository = repository;
    }

    /// <inheritdoc />
    public CosmeticsConfiguration? Configuration { get; set; }

    /// <summary>
    /// Gets the repository. It's resolved on each use, because the host may set it after the plugin has been created.
    /// </summary>
    internal IProgressionRepository Repository => this._repository ?? ProgressionRepositoryRegistry.Current ?? FallbackRepository;

    /// <summary>
    /// Gets the plugin of the game of the player, if it's active.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The plugin; <c>null</c>, if it's not active.</returns>
    public static CosmeticsPlugIn? GetActivePlugIn(Player player)
    {
        return GetActivePlugIn(player.GameContext);
    }

    /// <summary>
    /// Gets the plugin of the game context, if it's active.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The plugin; <c>null</c>, if it's not active.</returns>
    public static CosmeticsPlugIn? GetActivePlugIn(IGameContext gameContext)
    {
        return gameContext.PlugInManager.GetActivePlugInsOf<IPlayerStateChangedPlugIn>().OfType<CosmeticsPlugIn>().FirstOrDefault();
    }

    /// <summary>
    /// Gets the name of a piece which is shown: the configured one, or else the one of its item.
    /// </summary>
    /// <param name="gameConfiguration">The game configuration.</param>
    /// <param name="piece">The piece.</param>
    /// <param name="culture">The culture of the name of the item.</param>
    /// <returns>The name.</returns>
    public static string GetName(GameConfiguration gameConfiguration, CosmeticPieceDefinition piece, CultureInfo culture)
    {
        if (!string.IsNullOrWhiteSpace(piece.Name))
        {
            return piece.Name;
        }

        return PlayerCosmetics.GetItemDefinition(gameConfiguration, piece)?.Name.GetTranslation(culture)
               ?? string.Create(CultureInfo.InvariantCulture, $"{piece.Slot} {piece.ItemNumber}");
    }

    /// <inheritdoc />
    public object CreateDefaultConfig() => CosmeticsConfiguration.Default;

    /// <inheritdoc />
    public async ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        try
        {
            if (currentState.IsDisconnectedOrFinished() || currentState == PlayerState.CharacterSelection)
            {
                PlayerCosmetics.Set(player, []);
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

            // The players nearby got the character before its pieces were loaded.
            await this.ApplyWornPiecesAsync(player, state, true).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "Unexpected error handling the cosmetics at a player state change.");
        }
    }

    /// <summary>
    /// Sends the cosmetic pieces of the player to its client, e.g. when the client asks for its data again.
    /// </summary>
    /// <param name="player">The player.</param>
    public async ValueTask SendPiecesAsync(Player player)
    {
        if (await this.GetPiecesAsync(player).ConfigureAwait(false) is { } pieces)
        {
            await player.InvokeViewPlugInAsync<IShowCosmeticsPlugIn>(p => p.ShowCosmeticsAsync(pieces)).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Gets the configured pieces, with the information whether the character of the player unlocked and wears them.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The pieces; <c>null</c>, if they're not available.</returns>
    public async ValueTask<IReadOnlyList<CosmeticPieceEntry>?> GetPiecesAsync(Player player)
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

        var gameConfiguration = player.GameContext.Configuration;
        return configuration.Pieces
            .Where(p => p.Number > 0)
            .DistinctBy(p => p.Number)
            .OrderBy(p => p.Slot)
            .ThenBy(p => p.Number)
            .Select(p => new CosmeticPieceEntry(
                p,
                GetName(gameConfiguration, p, player.Culture),
                state.UnlockedPieceNumbers.Contains(p.Number),
                this.CanWear(player, p),
                state.WornPieceNumbers.TryGetValue(p.Slot, out var worn) && worn == p.Number))
            .ToList();
    }

    /// <summary>
    /// Puts on an unlocked piece over the item of its slot, instead of the piece which was worn there.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="pieceNumber">The number of the piece.</param>
    /// <returns>The result.</returns>
    public async ValueTask<CosmeticChangeResult> WearAsync(Player player, short pieceNumber)
    {
        if (this.Configuration is not { } configuration || this.GetOrCreateState(player) is not { } state)
        {
            return CosmeticChangeResult.NotAvailable;
        }

        if (configuration.FindPiece(pieceNumber) is not { } piece
            || PlayerCosmetics.GetItemDefinition(player.GameContext.Configuration, piece) is null)
        {
            return CosmeticChangeResult.Unknown;
        }

        if (!this.CanWear(player, piece))
        {
            return CosmeticChangeResult.NotQualified;
        }

        using (await state.Lock.LockAsync().ConfigureAwait(false))
        {
            if (!await this.EnsureLoadedAsync(player, state).ConfigureAwait(false))
            {
                return CosmeticChangeResult.NotAvailable;
            }

            if (!state.UnlockedPieceNumbers.Contains(piece.Number))
            {
                return CosmeticChangeResult.NotUnlocked;
            }

            if (!await this.TrySaveWornAsync(player, state, piece.Slot, piece.Number).ConfigureAwait(false))
            {
                return CosmeticChangeResult.NotAvailable;
            }
        }

        await this.ApplyWornPiecesAsync(player, state, false).ConfigureAwait(false);
        return CosmeticChangeResult.Worn;
    }

    /// <summary>
    /// Takes off a worn piece, or all of them.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="pieceNumber">The number of the piece; <c>null</c> to take off all pieces.</param>
    /// <returns>The result.</returns>
    public async ValueTask<CosmeticChangeResult> TakeOffAsync(Player player, short? pieceNumber)
    {
        if (this.GetOrCreateState(player) is not { } state)
        {
            return CosmeticChangeResult.NotAvailable;
        }

        using (await state.Lock.LockAsync().ConfigureAwait(false))
        {
            if (!await this.EnsureLoadedAsync(player, state).ConfigureAwait(false))
            {
                return CosmeticChangeResult.NotAvailable;
            }

            var slots = state.WornPieceNumbers
                .Where(worn => pieceNumber is null || worn.Value == pieceNumber)
                .Select(worn => worn.Key)
                .ToList();
            if (slots.Count == 0)
            {
                return CosmeticChangeResult.NotWorn;
            }

            foreach (var slot in slots)
            {
                if (!await this.TrySaveWornAsync(player, state, slot, null).ConfigureAwait(false))
                {
                    return CosmeticChangeResult.NotAvailable;
                }
            }
        }

        await this.ApplyWornPiecesAsync(player, state, false).ConfigureAwait(false);
        return CosmeticChangeResult.TakenOff;
    }

    /// <summary>
    /// Unlocks a cosmetic piece for the character of the player, e.g. as a reward.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="pieceNumber">The number of the piece.</param>
    /// <param name="source">Where the piece came from, e.g. "gm".</param>
    /// <returns>The piece, if it exists and could be unlocked or was already unlocked.</returns>
    public async ValueTask<CosmeticPieceDefinition?> GrantAsync(Player player, short pieceNumber, string source)
    {
        if (this.Configuration is not { } configuration
            || configuration.FindPiece(pieceNumber) is not { } piece
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

            if (state.UnlockedPieceNumbers.Contains(piece.Number))
            {
                return piece;
            }

            try
            {
                await this.Repository.AddUnlockedCosmeticPieceAsync(new UnlockedCosmeticPiece { CharacterId = state.CharacterId, PieceNumber = piece.Number, UnlockedAt = DateTime.UtcNow, Source = TruncateSource(source) }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                player.Logger.LogError(ex, "Couldn't save the unlocked cosmetic piece {piece} of character {characterId}.", piece.Number, state.CharacterId);
                return null;
            }

            state.UnlockedPieceNumbers.Add(piece.Number);
        }

        var name = GetName(player.GameContext.Configuration, piece, player.Culture);
        await player.InvokeViewPlugInAsync<IShowMessagePlugIn>(p => p.ShowMessageAsync(Format(configuration.UnlockedMessage, player, name, piece.Number), MessageType.GoldenCenter)).ConfigureAwait(false);
        await this.SendPiecesAsync(player).ConfigureAwait(false);
        return piece;
    }

    /// <summary>
    /// Removes an unlocked cosmetic piece from the character of the player, e.g. when it was given by mistake.
    /// If the character wears it, the real item of its slot is visible again.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="pieceNumber">The number of the piece.</param>
    /// <returns><c>true</c>, if the piece has been removed.</returns>
    public async ValueTask<bool> RevokeAsync(Player player, short pieceNumber)
    {
        if (this.GetOrCreateState(player) is not { } state)
        {
            return false;
        }

        bool removed;
        bool wasWorn;
        using (await state.Lock.LockAsync().ConfigureAwait(false))
        {
            if (!await this.EnsureLoadedAsync(player, state).ConfigureAwait(false))
            {
                return false;
            }

            // The repository takes the piece off, too.
            removed = await this.Repository.RemoveUnlockedCosmeticPieceAsync(state.CharacterId, pieceNumber).ConfigureAwait(false);
            state.UnlockedPieceNumbers.Remove(pieceNumber);
            var wornSlots = state.WornPieceNumbers.Where(worn => worn.Value == pieceNumber).Select(worn => worn.Key).ToList();
            wasWorn = wornSlots.Count > 0;
            wornSlots.ForEach(slot => state.WornPieceNumbers.Remove(slot));
        }

        if (wasWorn)
        {
            await this.ApplyWornPiecesAsync(player, state, false).ConfigureAwait(false);
        }
        else
        {
            await this.SendPiecesAsync(player).ConfigureAwait(false);
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

    private bool CanWear(Player player, CosmeticPieceDefinition piece)
    {
        if (this.Configuration is not { RestrictToQualifiedClasses: true })
        {
            return true;
        }

        return player.SelectedCharacter?.CharacterClass is { } characterClass
               && PlayerCosmetics.GetItemDefinition(player.GameContext.Configuration, piece) is { } definition
               && definition.QualifiedCharacters.Contains(characterClass);
    }

    private async ValueTask<bool> TrySaveWornAsync(Player player, CosmeticsPlayerState state, CosmeticSlot slot, short? pieceNumber)
    {
        try
        {
            await this.Repository.SetWornCosmeticPieceAsync(state.CharacterId, slot.GetItemSlot(), pieceNumber).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "Couldn't save the worn cosmetic pieces of character {characterId}.", state.CharacterId);
            return false;
        }

        if (pieceNumber is { } number)
        {
            state.WornPieceNumbers[slot] = number;
        }
        else
        {
            state.WornPieceNumbers.Remove(slot);
        }

        return true;
    }

    private CosmeticsPlayerState? GetOrCreateState(Player player)
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

        return States.GetValue(player, _ => new CosmeticsPlayerState(character.GetId()));
    }

    private async ValueTask<bool> EnsureLoadedAsync(Player player, CosmeticsPlayerState state)
    {
        if (state.IsLoaded)
        {
            return true;
        }

        try
        {
            var unlocked = await this.Repository.LoadUnlockedCosmeticPiecesAsync(state.CharacterId).ConfigureAwait(false);
            var worn = await this.Repository.LoadWornCosmeticPiecesAsync(state.CharacterId).ConfigureAwait(false);
            state.UnlockedPieceNumbers.Clear();
            state.UnlockedPieceNumbers.UnionWith(unlocked.Select(p => p.PieceNumber));
            state.WornPieceNumbers.Clear();
            foreach (var piece in worn)
            {
                if (CosmeticSlotExtensions.FromItemSlot(piece.ItemSlot) is { } slot)
                {
                    state.WornPieceNumbers[slot] = piece.PieceNumber;
                }
            }

            state.IsLoaded = true;
            return true;
        }
        catch (Exception ex)
        {
            player.Logger.LogWarning(ex, "Couldn't load the cosmetic pieces of character {characterId}.", state.CharacterId);
            return false;
        }
    }

    /// <summary>
    /// Applies the worn pieces to the appearance of the player, and shows them to the player and the players nearby.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="state">The state.</param>
    /// <param name="onlyIfWearing">If set to <c>true</c>, the observers are only updated when the player wears a piece, e.g. after entering the game.</param>
    private async ValueTask ApplyWornPiecesAsync(Player player, CosmeticsPlayerState state, bool onlyIfWearing)
    {
        // A piece which was removed from the configuration or which the class can't wear anymore isn't shown.
        var pieces = state.WornPieceNumbers.Values
            .Select(number => this.Configuration?.FindPiece(number))
            .OfType<CosmeticPieceDefinition>()
            .Where(piece => this.CanWear(player, piece))
            .ToList();

        var wasWearing = PlayerCosmetics.IsWearingAny(player);
        PlayerCosmetics.Set(player, pieces);
        await this.SendPiecesAsync(player).ConfigureAwait(false);
        if (pieces.Count == 0 && (onlyIfWearing || !wasWearing))
        {
            return;
        }

        player.OnAppearanceChanged();
        await player.ForEachWorldObserverAsync<INewPlayersInScopePlugIn>(p => p.NewPlayersInScopeAsync(player.GetAsEnumerable()), false).ConfigureAwait(false);
    }
}
