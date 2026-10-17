// <copyright file="PlayerCosmetics.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.CosmeticSets;

using System.Runtime.CompilerServices;
using MUnique.OpenMU.DataModel.Configuration.Items;

/// <summary>
/// Holds the cosmetic sets which the players in the game wear over their armor,
/// so that the appearance which other players see can be built from them.
/// </summary>
public static class PlayerCosmetics
{
    /// <summary>
    /// The item group of the helms. The groups of armors, pants, gloves and boots follow it.
    /// </summary>
    private const byte HelmGroup = 7;

    private static readonly byte[] ArmorSlots =
    [
        InventoryConstants.HelmSlot,
        InventoryConstants.ArmorSlot,
        InventoryConstants.PantsSlot,
        InventoryConstants.GlovesSlot,
        InventoryConstants.BootsSlot,
    ];

    private static readonly ConditionalWeakTable<Player, IReadOnlyDictionary<byte, ItemAppearance>> Appearances = new();

    /// <summary>
    /// Gets the appearance of the cosmetic piece which the player wears in a slot.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="itemSlot">The item slot.</param>
    /// <returns>The appearance; <c>null</c>, if the real item of the slot is visible.</returns>
    public static ItemAppearance? GetPiece(Player player, byte itemSlot)
    {
        return Appearances.TryGetValue(player, out var pieces) && pieces.TryGetValue(itemSlot, out var piece) ? piece : null;
    }

    /// <summary>
    /// Determines whether the player wears a cosmetic set.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns><c>true</c>, if the player wears a cosmetic set.</returns>
    public static bool IsWearingSet(Player player) => Appearances.TryGetValue(player, out _);

    /// <summary>
    /// Gets the item definitions of the pieces of a set, by item slot.
    /// </summary>
    /// <param name="gameConfiguration">The game configuration.</param>
    /// <param name="set">The set.</param>
    /// <returns>The item definitions of the slots for which the game has a piece.</returns>
    public static IReadOnlyDictionary<byte, ItemDefinition> GetPieceDefinitions(GameConfiguration gameConfiguration, CosmeticSetDefinition set)
    {
        var result = new Dictionary<byte, ItemDefinition>();
        foreach (var slot in ArmorSlots)
        {
            var group = (byte)(HelmGroup + slot - InventoryConstants.HelmSlot);
            if (gameConfiguration.Items.FirstOrDefault(i => i.Group == group && i.Number == set.ItemNumber) is { } definition)
            {
                result[slot] = definition;
            }
        }

        return result;
    }

    /// <summary>
    /// Sets the cosmetic set which the player wears.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="set">The set; <c>null</c> to wear none.</param>
    internal static void Set(Player player, CosmeticSetDefinition? set)
    {
        if (set is null)
        {
            Appearances.Remove(player);
            return;
        }

        var pieces = new Dictionary<byte, ItemAppearance>();
        foreach (var (slot, definition) in GetPieceDefinitions(player.GameContext.Configuration, set))
        {
            var piece = new CosmeticItemAppearance { ItemSlot = slot, Definition = definition, Level = set.Level };
            if (set.IsExcellent)
            {
                piece.VisibleOptions.Add(ItemOptionTypes.Excellent);
            }

            pieces[slot] = piece;
        }

        Appearances.AddOrUpdate(player, pieces);
    }

    private sealed class CosmeticItemAppearance : ItemAppearance
    {
        public override ICollection<ItemOptionType> VisibleOptions => base.VisibleOptions ??= new List<ItemOptionType>();
    }
}
