// <copyright file="BossEncounterRewarder.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.BossEncounters;

using MUnique.OpenMU.GameLogic.Views.Inventory;

/// <summary>
/// Gives the rewards for the participation in a boss encounter.
/// </summary>
internal static class BossEncounterRewarder
{
    /// <summary>
    /// Gives the items and the money to the player. An item which doesn't fit into the inventory is
    /// dropped at the feet of the player, and only the player can pick it up.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="items">The items.</param>
    /// <param name="money">The money.</param>
    public static async ValueTask GiveAsync(Player player, IEnumerable<Item> items, int money)
    {
        foreach (var item in items)
        {
            await GiveItemAsync(player, item).ConfigureAwait(false);
        }

        if (money > 0 && !player.TryAddMoney(money))
        {
            player.Logger.LogWarning("The zen of the boss encounter reward didn't fit into the inventory of {Player}.", player);
        }
    }

    private static async ValueTask GiveItemAsync(Player player, Item item)
    {
        if (item is TemporaryItem temporaryItem && player.Inventory is { } inventory)
        {
            var persistentItem = temporaryItem.MakePersistent(player.PersistenceContext);
            if (await inventory.AddItemAsync(persistentItem).ConfigureAwait(false))
            {
                await player.InvokeViewPlugInAsync<IItemAppearPlugIn>(p => p.ItemAppearAsync(persistentItem)).ConfigureAwait(false);
                return;
            }

            await player.PersistenceContext.DeleteAsync(persistentItem).ConfigureAwait(false);
        }

        if (player.CurrentMap is { } map)
        {
            await map.AddAsync(new DroppedItem(item, player.Position, map, player, player.GetAsEnumerable())).ConfigureAwait(false);
        }
    }
}
