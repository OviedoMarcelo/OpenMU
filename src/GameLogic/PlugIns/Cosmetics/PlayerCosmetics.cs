// <copyright file="PlayerCosmetics.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.Cosmetics;

using System.Runtime.CompilerServices;
using MUnique.OpenMU.DataModel.Configuration.Items;

/// <summary>
/// Holds the cosmetic pieces which the players in the game wear over their armor, wings and pet,
/// so that the appearance which other players see can be built from them.
/// </summary>
public static class PlayerCosmetics
{
    private static readonly ConditionalWeakTable<Player, IReadOnlyDictionary<byte, WornAppearance>> Appearances = new();

    /// <summary>
    /// Gets the appearance of the cosmetic piece which the player wears in a slot, if it's shown: a piece which is
    /// only shown over a certain item (e.g. the skin of the Guardian Angel) is only shown while the player has it there.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="itemSlot">The item slot.</param>
    /// <returns>The appearance; <c>null</c>, if the real item of the slot is visible.</returns>
    public static ItemAppearance? GetPiece(Player player, byte itemSlot)
    {
        if (!Appearances.TryGetValue(player, out var pieces) || !pieces.TryGetValue(itemSlot, out var piece))
        {
            return null;
        }

        if (piece.OnlyOverItemNumber is not { } number)
        {
            return piece.Appearance;
        }

        var item = player.Inventory?.GetItem(itemSlot)?.Definition;
        return item?.Group == piece.Appearance.Definition?.Group && item?.Number == number ? piece.Appearance : null;
    }

    /// <summary>
    /// Determines whether the player wears any cosmetic piece.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns><c>true</c>, if the player wears a cosmetic piece.</returns>
    public static bool IsWearingAny(Player player) => Appearances.TryGetValue(player, out _);

    /// <summary>
    /// Gets the item definition which a piece shows.
    /// </summary>
    /// <param name="gameConfiguration">The game configuration.</param>
    /// <param name="piece">The piece.</param>
    /// <returns>The item definition; <c>null</c>, if the game has no such item.</returns>
    public static ItemDefinition? GetItemDefinition(GameConfiguration gameConfiguration, CosmeticPieceDefinition piece)
    {
        var group = piece.Slot.GetItemGroup();
        return gameConfiguration.Items.FirstOrDefault(i => i.Group == group && i.Number == piece.ItemNumber);
    }

    /// <summary>
    /// Sets the cosmetic pieces which the player wears.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="pieces">The pieces, at most one per slot.</param>
    internal static void Set(Player player, IEnumerable<CosmeticPieceDefinition> pieces)
    {
        var appearances = new Dictionary<byte, WornAppearance>();
        foreach (var piece in pieces)
        {
            if (GetItemDefinition(player.GameContext.Configuration, piece) is not { } definition)
            {
                continue;
            }

            var slot = piece.Slot.GetItemSlot();
            var appearance = new CosmeticItemAppearance { ItemSlot = slot, Definition = definition, Level = piece.Level };
            if (piece.IsExcellent)
            {
                appearance.VisibleOptions.Add(ItemOptionTypes.Excellent);
            }

            appearances[slot] = new WornAppearance(appearance, piece.OnlyOverItemNumber);
        }

        if (appearances.Count == 0)
        {
            Appearances.Remove(player);
        }
        else
        {
            Appearances.AddOrUpdate(player, appearances);
        }
    }

    /// <summary>
    /// The appearance of a worn piece.
    /// </summary>
    /// <param name="Appearance">The appearance.</param>
    /// <param name="OnlyOverItemNumber">The number of the item over which it's only shown; <c>null</c>: always.</param>
    private sealed record WornAppearance(ItemAppearance Appearance, short? OnlyOverItemNumber);

    private sealed class CosmeticItemAppearance : ItemAppearance
    {
        public override ICollection<ItemOptionType> VisibleOptions => base.VisibleOptions ??= new List<ItemOptionType>();
    }
}
