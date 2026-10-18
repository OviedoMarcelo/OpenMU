// <copyright file="CosmeticsManagementService.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.Services;

using System.Globalization;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlugIns.Cosmetics;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.Progression;

/// <summary>
/// Gives the admin panel access to the cosmetic pieces of the characters: which ones they unlocked and wear,
/// and giving or removing them.
/// </summary>
/// <remarks>
/// When the character is in the game, the changes go through the <see cref="CosmeticsPlugIn"/>, so that they
/// apply immediately and the players nearby see them. Otherwise, they're written to the storage and apply
/// when the character enters the game the next time.
/// </remarks>
public sealed class CosmeticsManagementService
{
    private const string AdminSource = "admin";

    private static readonly CultureInfo NameCulture = CultureInfo.GetCultureInfo("es");

    private readonly IServerProvider _serverProvider;
    private readonly ILogger<CosmeticsManagementService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CosmeticsManagementService"/> class.
    /// </summary>
    /// <param name="serverProvider">The provider of manageable servers.</param>
    /// <param name="logger">The logger.</param>
    public CosmeticsManagementService(IServerProvider serverProvider, ILogger<CosmeticsManagementService> logger)
    {
        this._serverProvider = serverProvider;
        this._logger = logger;
    }

    private IEnumerable<IGameServerContext> Contexts => this._serverProvider.Servers
        .OfType<IGameServerContextProvider>()
        .Select(provider => provider.Context);

    /// <summary>
    /// Gets the cosmetic pieces of a character.
    /// </summary>
    /// <param name="characterId">The identifier of the character.</param>
    /// <returns>The pieces, or the error.</returns>
    public async ValueTask<(CharacterCosmetics? Cosmetics, CosmeticsAdministrationError Error)> GetAsync(Guid characterId)
    {
        if (this.FindPlugIn() is not { PlugIn.Configuration: { } configuration, Context: var context } || ProgressionRepositoryRegistry.Current is not { } repository)
        {
            return (null, CosmeticsAdministrationError.PlugInInactive);
        }

        try
        {
            var player = await this.FindPlayerAsync(characterId).ConfigureAwait(false);
            var unlocked = (await repository.LoadUnlockedCosmeticPiecesAsync(characterId).ConfigureAwait(false)).Select(p => p.PieceNumber).ToHashSet();
            var worn = (await repository.LoadWornCosmeticPiecesAsync(characterId).ConfigureAwait(false))
                .Where(p => CosmeticSlotExtensions.FromItemSlot(p.ItemSlot) is not null)
                .ToDictionary(p => CosmeticSlotExtensions.FromItemSlot(p.ItemSlot)!.Value, p => p.PieceNumber);
            var pieces = configuration.Pieces
                .Where(p => p.Number > 0)
                .DistinctBy(p => p.Number)
                .OrderBy(p => p.Slot)
                .ThenBy(p => p.Number)
                .Select(p => new CosmeticPieceItem(
                    p.Number,
                    CosmeticsPlugIn.GetName(context.Configuration, p, NameCulture),
                    p.Slot.ToString(),
                    p.Slot.GetItemGroup(),
                    p.ItemNumber,
                    p.Level,
                    p.IsExcellent,
                    unlocked.Contains(p.Number),
                    worn.TryGetValue(p.Slot, out var number) && number == p.Number))
                .ToList();
            return (new CharacterCosmetics(player is not null, pieces), CosmeticsAdministrationError.None);
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Couldn't load the cosmetic pieces of character {characterId}.", characterId);
            return (null, CosmeticsAdministrationError.StorageUnavailable);
        }
    }

    /// <summary>
    /// Unlocks a cosmetic piece for a character.
    /// </summary>
    /// <param name="characterId">The identifier of the character.</param>
    /// <param name="pieceNumber">The number of the piece.</param>
    /// <returns>The error; <see cref="CosmeticsAdministrationError.None"/>, if the piece has been unlocked.</returns>
    public async ValueTask<CosmeticsAdministrationError> GrantAsync(Guid characterId, short pieceNumber)
    {
        if (this.FindPlugIn() is not { PlugIn: { Configuration: { } configuration } plugIn } || ProgressionRepositoryRegistry.Current is not { } repository)
        {
            return CosmeticsAdministrationError.PlugInInactive;
        }

        if (configuration.FindPiece(pieceNumber) is null)
        {
            return CosmeticsAdministrationError.PieceNotFound;
        }

        try
        {
            if (await this.FindPlayerAsync(characterId).ConfigureAwait(false) is { } player)
            {
                return await plugIn.GrantAsync(player, pieceNumber, AdminSource).ConfigureAwait(false) is null
                    ? CosmeticsAdministrationError.StorageUnavailable
                    : CosmeticsAdministrationError.None;
            }

            await repository.AddUnlockedCosmeticPieceAsync(new UnlockedCosmeticPiece { CharacterId = characterId, PieceNumber = pieceNumber, UnlockedAt = DateTime.UtcNow, Source = AdminSource }).ConfigureAwait(false);
            return CosmeticsAdministrationError.None;
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Couldn't give the cosmetic piece {piece} to character {characterId}.", pieceNumber, characterId);
            return CosmeticsAdministrationError.StorageUnavailable;
        }
    }

    /// <summary>
    /// Removes an unlocked cosmetic piece from a character. If the character wears it, it's taken off.
    /// </summary>
    /// <param name="characterId">The identifier of the character.</param>
    /// <param name="pieceNumber">The number of the piece.</param>
    /// <returns>The error; <see cref="CosmeticsAdministrationError.None"/>, if the piece has been removed.</returns>
    public async ValueTask<CosmeticsAdministrationError> RevokeAsync(Guid characterId, short pieceNumber)
    {
        if (this.FindPlugIn() is not { PlugIn: var plugIn } || ProgressionRepositoryRegistry.Current is not { } repository)
        {
            return CosmeticsAdministrationError.PlugInInactive;
        }

        try
        {
            var removed = await this.FindPlayerAsync(characterId).ConfigureAwait(false) is { } player
                ? await plugIn.RevokeAsync(player, pieceNumber).ConfigureAwait(false)
                : await repository.RemoveUnlockedCosmeticPieceAsync(characterId, pieceNumber).ConfigureAwait(false);
            return removed ? CosmeticsAdministrationError.None : CosmeticsAdministrationError.NotUnlocked;
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Couldn't remove the cosmetic piece {piece} of character {characterId}.", pieceNumber, characterId);
            return CosmeticsAdministrationError.StorageUnavailable;
        }
    }

    private (CosmeticsPlugIn PlugIn, IGameServerContext Context)? FindPlugIn()
    {
        foreach (var context in this.Contexts)
        {
            if (CosmeticsPlugIn.GetActivePlugIn(context) is { } plugIn)
            {
                return (plugIn, context);
            }
        }

        return null;
    }

    private async ValueTask<Player?> FindPlayerAsync(Guid characterId)
    {
        foreach (var context in this.Contexts)
        {
            var players = await context.GetPlayersAsync().ConfigureAwait(false);
            if (players.FirstOrDefault(p => p.SelectedCharacter is { } character && character.GetId() == characterId) is { } player)
            {
                return player;
            }
        }

        return null;
    }
}
