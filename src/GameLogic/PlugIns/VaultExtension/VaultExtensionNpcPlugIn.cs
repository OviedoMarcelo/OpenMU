// <copyright file="VaultExtensionNpcPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.VaultExtension;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.Views;
using MUnique.OpenMU.GameLogic.Views.Inventory;
using MUnique.OpenMU.GameLogic.Views.NPC;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// An NPC which extends the vault of the account (doubles its size, see <see cref="Account.IsVaultExtended"/>)
/// in exchange for some items, e.g. packs of jewels. The player confirms it in a dialog.
/// </summary>
[PlugIn]
[Display(Name = "Vault Extension NPC", Description = "Un NPC (el Guardián del Baúl) que extiende el baúl de la cuenta a cambio de ítems, por ejemplo paquetes de joyas.")]
[Guid("6DF9643D-2BF9-485B-A280-E530CC19E7B8")]
public class VaultExtensionNpcPlugIn : IPlayerTalkToNpcPlugIn, ISupportCustomConfiguration<VaultExtensionConfiguration>, ISupportDefaultCustomConfiguration
{
    private const string DialogTitle = "Baúl extendido";

    /// <inheritdoc />
    public VaultExtensionConfiguration? Configuration { get; set; }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new VaultExtensionConfiguration();

    /// <inheritdoc />
    public ValueTask PlayerTalksToNpcAsync(Player player, NonPlayerCharacter npc, NpcTalkEventArgs eventArgs)
    {
        // It has to happen synchronously, because the caller doesn't await this method.
        if (this.Configuration is not { } configuration || npc.Definition.Number != configuration.NpcNumber)
        {
            return ValueTask.CompletedTask;
        }

        eventArgs.HasBeenHandled = true;
        _ = this.TalkAsync(player, npc, configuration);
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Gets the items of the inventory which the player gives for the price, or the captions of the missing ones.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The items, and the captions of the missing ones.</returns>
    internal static (List<Item> Items, List<string> Missing) FindPrice(Player player, VaultExtensionConfiguration configuration)
    {
        // Equipped items and the ones of the personal store don't count.
        var available = player.Inventory?.Items
            .Where(item => item.ItemSlot >= InventoryConstants.EquippableSlotsCount && item.ItemSlot < InventoryConstants.FirstStoreItemSlotIndex)
            .ToList() ?? [];
        var items = new List<Item>();
        var missing = new List<string>();
        foreach (var price in configuration.Price)
        {
            var matching = available
                .Where(item => item.Definition?.Group == price.ItemGroup && item.Definition.Number == price.ItemNumber && item.Level == price.ItemLevel)
                .Take(price.Amount)
                .ToList();
            if (matching.Count < price.Amount)
            {
                missing.Add(Describe(price));
                continue;
            }

            items.AddRange(matching);
            matching.ForEach(item => available.Remove(item));
        }

        return (items, missing);
    }

    private static string Describe(VaultExtensionPrice price)
    {
        var caption = string.IsNullOrWhiteSpace(price.Caption) ? $"Ítem {price.ItemGroup}/{price.ItemNumber} +{price.ItemLevel}" : price.Caption;
        return $"{price.Amount} {caption}";
    }

    private static ValueTask SayAsync(Player player, NonPlayerCharacter npc, string text)
    {
        return player.InvokeViewPlugInAsync<IShowMessageOfObjectPlugIn>(p => p.ShowMessageOfObjectAsync(text, npc));
    }

    private async Task TalkAsync(Player player, NonPlayerCharacter npc, VaultExtensionConfiguration configuration)
    {
        try
        {
            if (player.Account is not { } account)
            {
                return;
            }

            if (account.IsVaultExtended)
            {
                await SayAsync(player, npc, "Tu baúl ya está extendido. ¡Que lo disfrutes!").ConfigureAwait(false);
                return;
            }

            if (!NpcDialogs.IsSupported(player))
            {
                await player.ShowBlueMessageAsync("Actualizá el juego para poder extender tu baúl.").ConfigureAwait(false);
                return;
            }

            var text = "Puedo duplicar el lugar de tu baúl, para siempre y para todos los personajes de tu cuenta.\n\n"
                       + "A cambio necesito que tengas en el inventario:\n"
                       + string.Join('\n', configuration.Price.Select(price => "- " + Describe(price)))
                       + "\n\n¿Querés extenderlo?";
            await NpcDialogs.ShowAsync(player, npc, DialogTitle, text, this.ExtendVaultAsync).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "Unexpected error when talking to the NPC which extends the vault.");
        }
    }

    private async ValueTask ExtendVaultAsync(Player player, NonPlayerCharacter npc)
    {
        if (this.Configuration is not { } configuration || player.Account is not { } account || player.Inventory is not { } inventory)
        {
            return;
        }

        if (account.IsVaultExtended)
        {
            await SayAsync(player, npc, "Tu baúl ya está extendido. ¡Que lo disfrutes!").ConfigureAwait(false);
            return;
        }

        var (items, missing) = FindPrice(player, configuration);
        if (missing.Count > 0)
        {
            await SayAsync(player, npc, "Todavía te falta algo para extender tu baúl.").ConfigureAwait(false);
            await player.ShowBlueMessageAsync("Te falta en el inventario: " + string.Join(", ", missing) + ".").ConfigureAwait(false);
            return;
        }

        foreach (var item in items)
        {
            await inventory.RemoveItemAsync(item).ConfigureAwait(false);
            await player.PersistenceContext.DeleteAsync(item).ConfigureAwait(false);
            await player.InvokeViewPlugInAsync<IItemRemovedPlugIn>(p => p.RemoveItemAsync(item.ItemSlot)).ConfigureAwait(false);
        }

        account.IsVaultExtended = true;
        player.Logger.LogInformation("The vault of account {account} was extended by the NPC, for {items}.", account.LoginName, string.Join(", ", items.Select(i => $"{i.Definition?.Name} +{i.Level}")));

        // Saved right away, so that the items and the extension don't get lost apart.
        await player.SaveProgressAsync().ConfigureAwait(false);

        await SayAsync(player, npc, "¡Listo! Tu baúl ahora tiene el doble de lugar.").ConfigureAwait(false);
        await player.ShowBlueMessageAsync("Tu baúl ya está extendido. Para usar su segunda parte (tecla H con el baúl abierto), volvé a la selección de personaje.").ConfigureAwait(false);
    }
}
