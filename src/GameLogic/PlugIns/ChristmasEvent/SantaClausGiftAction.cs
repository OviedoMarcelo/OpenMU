// <copyright file="SantaClausGiftAction.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChristmasEvent;

using System.Globalization;
using MUnique.OpenMU.GameLogic.Properties;
using MUnique.OpenMU.GameLogic.Views.Inventory;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.Progression;

/// <summary>
/// The action when a player asks Santa Claus for the gift: once per day for each account and, if configured,
/// for each IP address (each household), see <see cref="ChristmasEventConfiguration.LimitPerIpAddress"/>.
/// </summary>
public class SantaClausGiftAction
{
    /// <summary>
    /// The prefix of the keys of the claims of the gift.
    /// </summary>
    internal const string ClaimKeyPrefix = "santa-gift";

    /// <summary>
    /// The repository which is used when no other is registered, e.g. in the demo mode.
    /// </summary>
    private static readonly InMemoryProgressionRepository FallbackRepository = new();

    private readonly IProgressionRepository? _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="SantaClausGiftAction"/> class.
    /// </summary>
    public SantaClausGiftAction()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SantaClausGiftAction"/> class.
    /// </summary>
    /// <param name="repository">The repository of the claims.</param>
    internal SantaClausGiftAction(IProgressionRepository repository)
    {
        this._repository = repository;
    }

    private IProgressionRepository Repository => this._repository ?? ProgressionRepositoryRegistry.Current ?? FallbackRepository;

    /// <summary>
    /// Gives the gift of Santa Claus to the player, if the player didn't get it today yet.
    /// </summary>
    /// <param name="player">The player.</param>
    public async ValueTask RequestGiftAsync(Player player)
    {
        if (player.Account is not { } account
            || player.SelectedCharacter is not { } character
            || player.Inventory is not { } inventory
            || player.CurrentMap is not { } map)
        {
            return;
        }

        var gameContext = player.GameContext;
        var serverTime = ChristmasEventPlugIn.GetServerTime(gameContext);
        if (ChristmasEventPlugIn.GetConfiguration(gameContext) is not { } configuration || !configuration.IsRunning(serverTime))
        {
            await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.ChristmasEventNotRunning)).ConfigureAwait(false);
            return;
        }

        // The client sends the request from the dialog of Santa Claus, so the player has to stand next to him.
        if (map.MapId != ChristmasEventPlugIn.SantaVillageMapNumber
            || !map.GetNpcsInRange(player.Position, ChristmasEventPlugIn.SantaClausRange).Any(npc => npc.Definition.Number == ChristmasEventPlugIn.SantaClausNumber))
        {
            return;
        }

        if (gameContext.Configuration.Items.FirstOrDefault(i => i.Group == configuration.GiftItemGroup && i.Number == configuration.GiftItemNumber) is not { } giftDefinition)
        {
            player.Logger.LogWarning("The gift of Santa Claus ({group}, {number}) doesn't exist.", configuration.GiftItemGroup, configuration.GiftItemNumber);
            await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.SantaGiftFailed)).ConfigureAwait(false);
            return;
        }

        var gift = ConfiguredItemFactory.Create(giftDefinition, configuration.GiftItemLevel, false, false, 0, 0);
        if (inventory.CheckInvSpace(gift) is null)
        {
            await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.SantaGiftInventoryFull)).ConfigureAwait(false);
            return;
        }

        var ipAddress = configuration.LimitPerIpAddress ? (player as IHasIpAddress)?.IpAddress : null;
        var claims = GetClaimKeys(DateOnly.FromDateTime(serverTime), account.GetId(), ipAddress)
            .Select(key => new EventClaim { Key = key, ClaimedAt = DateTime.UtcNow, AccountId = account.GetId(), CharacterId = character.Id })
            .ToList();
        bool claimed;
        try
        {
            claimed = await this.Repository.TryAddEventClaimsAsync(claims).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "Couldn't save the gift of Santa Claus for {account}.", account.LoginName);
            await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.SantaGiftFailed)).ConfigureAwait(false);
            return;
        }

        if (!claimed)
        {
            await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.SantaGiftAlreadyClaimed)).ConfigureAwait(false);
            return;
        }

        var item = gift.MakePersistent(player.PersistenceContext);
        if (!await inventory.AddItemAsync(item).ConfigureAwait(false))
        {
            // The space has been checked, so this shouldn't happen; the claim stays, so it's logged.
            await player.PersistenceContext.DeleteAsync(item).ConfigureAwait(false);
            player.Logger.LogError("Couldn't add the gift of Santa Claus to the inventory of {character}, although there was space.", character.Name);
            await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.SantaGiftFailed)).ConfigureAwait(false);
            return;
        }

        await player.InvokeViewPlugInAsync<IItemAppearPlugIn>(p => p.ItemAppearAsync(item)).ConfigureAwait(false);
        await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.SantaGiftReceived)).ConfigureAwait(false);
        player.Logger.LogInformation("Santa Claus gave {item} +{level} to {character} ({account}, {ip}).", giftDefinition.Name, item.Level, character.Name, account.LoginName, ipAddress);
    }

    /// <summary>
    /// Gets the keys which claim the gift of a day, one for the account and one for the IP address.
    /// </summary>
    /// <param name="day">The day, in the time zone of the server.</param>
    /// <param name="accountId">The identifier of the account.</param>
    /// <param name="ipAddress">The IP address, if it's limited per IP address.</param>
    /// <returns>The keys.</returns>
    internal static IEnumerable<string> GetClaimKeys(DateOnly day, Guid accountId, string? ipAddress)
    {
        var dayText = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        yield return $"{ClaimKeyPrefix}:{dayText}:account:{accountId}";
        if (!string.IsNullOrWhiteSpace(ipAddress))
        {
            yield return $"{ClaimKeyPrefix}:{dayText}:ip:{ipAddress}";
        }
    }
}
