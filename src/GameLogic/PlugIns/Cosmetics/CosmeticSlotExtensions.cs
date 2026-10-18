// <copyright file="CosmeticSlotExtensions.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.Cosmetics;

/// <summary>
/// Extensions for the <see cref="CosmeticSlot"/>.
/// </summary>
public static class CosmeticSlotExtensions
{
    /// <summary>
    /// The item group of the helms. The groups of armors, pants, gloves and boots follow it.
    /// </summary>
    private const byte HelmGroup = 7;

    /// <summary>
    /// Gets all slots which can be covered.
    /// </summary>
    public static IReadOnlyList<CosmeticSlot> All { get; } = Enum.GetValues<CosmeticSlot>();

    /// <summary>
    /// Gets the item group of the items of the slot.
    /// </summary>
    /// <param name="slot">The slot.</param>
    /// <returns>The item group, e.g. 7 for the helm.</returns>
    public static byte GetItemGroup(this CosmeticSlot slot) => (byte)(HelmGroup + (int)slot);

    /// <summary>
    /// Gets the inventory slot of the items of the slot.
    /// </summary>
    /// <param name="slot">The slot.</param>
    /// <returns>The inventory slot, e.g. the one of the helm.</returns>
    public static byte GetItemSlot(this CosmeticSlot slot) => (byte)(InventoryConstants.HelmSlot + (int)slot);

    /// <summary>
    /// Gets the slot which covers an inventory slot.
    /// </summary>
    /// <param name="itemSlot">The inventory slot.</param>
    /// <returns>The slot; <c>null</c>, if the inventory slot isn't one of the armor.</returns>
    public static CosmeticSlot? FromItemSlot(byte itemSlot)
    {
        var index = itemSlot - InventoryConstants.HelmSlot;
        return index >= 0 && index < All.Count ? (CosmeticSlot)index : null;
    }
}
