// <copyright file="SpentItems.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic;

using MUnique.OpenMU.Persistence;

/// <summary>
/// Finds and removes items which are used up but still stored, e.g. a consumed jewel whose
/// deletion didn't reach the database. Such an item has no pieces left, so it can't be used
/// anymore, but it still blocks consumption (e.g. the potion hotkey picks it first).
/// </summary>
/// <remarks>
/// Only items which aren't wearable count: their durability is the number of pieces. For
/// wearable items, a durability of 0 means broken, which is a valid state.
/// </remarks>
public static class SpentItems
{
    /// <summary>
    /// Determines whether the item is used up: it isn't wearable, its definition has pieces,
    /// and it has none left.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns><c>true</c>, if the item is used up.</returns>
    public static bool IsSpent(Item item)
    {
        return item.Definition is { Durability: > 0 }
               && !item.IsWearable()
               && item.Durability <= 0;
    }

    /// <summary>
    /// Removes the used up items of the storage, before it's loaded into the game.
    /// </summary>
    /// <param name="storage">The storage, e.g. the inventory of the character or the vault of the account.</param>
    /// <param name="persistenceContext">The persistence context which tracks the items.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="owner">The description of the owner, for the log.</param>
    /// <returns>The number of removed items.</returns>
    public static async ValueTask<int> RemoveFromStorageAsync(ItemStorage? storage, IContext persistenceContext, ILogger logger, string owner)
    {
        if (storage is null)
        {
            return 0;
        }

        var spentItems = storage.Items.Where(IsSpent).ToList();
        foreach (var item in spentItems)
        {
            logger.LogWarning(
                "Removing the used up item {item} (id {id}, slot {slot}) of {owner}; its consumption wasn't saved completely.",
                item.Definition?.Name,
                item.GetId(),
                item.ItemSlot,
                owner);
            storage.Items.Remove(item);
            await persistenceContext.DeleteAsync(item).ConfigureAwait(false);
        }

        return spentItems.Count;
    }
}
