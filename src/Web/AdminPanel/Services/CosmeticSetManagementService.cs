// <copyright file="CosmeticSetManagementService.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.Services;

using Microsoft.Extensions.Logging;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlugIns.CosmeticSets;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.Progression;

/// <summary>
/// Gives the admin panel access to the cosmetic sets of the characters: which ones they unlocked and wear,
/// and giving or removing them.
/// </summary>
/// <remarks>
/// When the character is in the game, the changes go through the <see cref="CosmeticSetsPlugIn"/>, so that they
/// apply immediately and the players nearby see them. Otherwise, they're written to the storage and apply
/// when the character enters the game the next time.
/// </remarks>
public sealed class CosmeticSetManagementService
{
    private const string AdminSource = "admin";

    private readonly IServerProvider _serverProvider;
    private readonly ILogger<CosmeticSetManagementService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CosmeticSetManagementService"/> class.
    /// </summary>
    /// <param name="serverProvider">The provider of manageable servers.</param>
    /// <param name="logger">The logger.</param>
    public CosmeticSetManagementService(IServerProvider serverProvider, ILogger<CosmeticSetManagementService> logger)
    {
        this._serverProvider = serverProvider;
        this._logger = logger;
    }

    private IEnumerable<IGameServerContext> Contexts => this._serverProvider.Servers
        .OfType<IGameServerContextProvider>()
        .Select(provider => provider.Context);

    /// <summary>
    /// Gets the cosmetic sets of a character.
    /// </summary>
    /// <param name="characterId">The identifier of the character.</param>
    /// <returns>The sets, or the error.</returns>
    public async ValueTask<(CharacterCosmeticSets? Sets, CosmeticSetAdministrationError Error)> GetAsync(Guid characterId)
    {
        if (this.FindPlugIn() is not { Configuration: { } configuration } || ProgressionRepositoryRegistry.Current is not { } repository)
        {
            return (null, CosmeticSetAdministrationError.PlugInInactive);
        }

        try
        {
            var player = await this.FindPlayerAsync(characterId).ConfigureAwait(false);
            var unlocked = (await repository.LoadUnlockedCosmeticSetsAsync(characterId).ConfigureAwait(false)).Select(s => s.SetNumber).ToHashSet();
            var active = await repository.LoadActiveCosmeticSetAsync(characterId).ConfigureAwait(false);
            var sets = configuration.Sets
                .Where(s => s.Number > 0)
                .DistinctBy(s => s.Number)
                .OrderBy(s => s.Number)
                .Select(s => new CosmeticSetItem(s.Number, s.Name, s.ItemNumber, s.Level, s.IsExcellent, unlocked.Contains(s.Number), s.Number == active))
                .ToList();
            return (new CharacterCosmeticSets(player is not null, sets), CosmeticSetAdministrationError.None);
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Couldn't load the cosmetic sets of character {characterId}.", characterId);
            return (null, CosmeticSetAdministrationError.StorageUnavailable);
        }
    }

    /// <summary>
    /// Unlocks a cosmetic set for a character.
    /// </summary>
    /// <param name="characterId">The identifier of the character.</param>
    /// <param name="setNumber">The number of the set.</param>
    /// <returns>The error; <see cref="CosmeticSetAdministrationError.None"/>, if the set has been unlocked.</returns>
    public async ValueTask<CosmeticSetAdministrationError> GrantAsync(Guid characterId, short setNumber)
    {
        if (this.FindPlugIn() is not { Configuration: { } configuration } plugIn || ProgressionRepositoryRegistry.Current is not { } repository)
        {
            return CosmeticSetAdministrationError.PlugInInactive;
        }

        if (configuration.FindSet(setNumber) is null)
        {
            return CosmeticSetAdministrationError.SetNotFound;
        }

        try
        {
            if (await this.FindPlayerAsync(characterId).ConfigureAwait(false) is { } player)
            {
                return await plugIn.GrantAsync(player, setNumber, AdminSource).ConfigureAwait(false) is null
                    ? CosmeticSetAdministrationError.StorageUnavailable
                    : CosmeticSetAdministrationError.None;
            }

            await repository.AddUnlockedCosmeticSetAsync(new UnlockedCosmeticSet { CharacterId = characterId, SetNumber = setNumber, UnlockedAt = DateTime.UtcNow, Source = AdminSource }).ConfigureAwait(false);
            return CosmeticSetAdministrationError.None;
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Couldn't give the cosmetic set {set} to character {characterId}.", setNumber, characterId);
            return CosmeticSetAdministrationError.StorageUnavailable;
        }
    }

    /// <summary>
    /// Removes an unlocked cosmetic set from a character. If the character wears it, it wears none afterwards.
    /// </summary>
    /// <param name="characterId">The identifier of the character.</param>
    /// <param name="setNumber">The number of the set.</param>
    /// <returns>The error; <see cref="CosmeticSetAdministrationError.None"/>, if the set has been removed.</returns>
    public async ValueTask<CosmeticSetAdministrationError> RevokeAsync(Guid characterId, short setNumber)
    {
        if (this.FindPlugIn() is not { } plugIn || ProgressionRepositoryRegistry.Current is not { } repository)
        {
            return CosmeticSetAdministrationError.PlugInInactive;
        }

        try
        {
            if (await this.FindPlayerAsync(characterId).ConfigureAwait(false) is { } player)
            {
                return await plugIn.RevokeAsync(player, setNumber).ConfigureAwait(false)
                    ? CosmeticSetAdministrationError.None
                    : CosmeticSetAdministrationError.NotUnlocked;
            }

            if (!await repository.RemoveUnlockedCosmeticSetAsync(characterId, setNumber).ConfigureAwait(false))
            {
                return CosmeticSetAdministrationError.NotUnlocked;
            }

            if (await repository.LoadActiveCosmeticSetAsync(characterId).ConfigureAwait(false) == setNumber)
            {
                await repository.SetActiveCosmeticSetAsync(characterId, null).ConfigureAwait(false);
            }

            return CosmeticSetAdministrationError.None;
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Couldn't remove the cosmetic set {set} of character {characterId}.", setNumber, characterId);
            return CosmeticSetAdministrationError.StorageUnavailable;
        }
    }

    private CosmeticSetsPlugIn? FindPlugIn()
    {
        return this.Contexts.Select(CosmeticSetsPlugIn.GetActivePlugIn).FirstOrDefault(plugIn => plugIn is not null);
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
