// <copyright file="SkinArmorSetBuilder.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Items;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.Persistence.Initialization.CharacterClasses;

/// <summary>
/// Builds the armor sets of skin packs: a helm, armor, pants, gloves and boots with the same item number,
/// which every class can wear. They are only skins: the characters wear them as cosmetic pieces over their
/// real armor, so the pieces have no stats (no defense, options or set bonuses) and don't drop from monsters.
/// </summary>
/// <remarks>
/// The item definitions are still needed: the cosmetic pieces take their names and classes from them, and the
/// client draws the model of the item number. A piece which a game master creates is worn like any armor, but
/// without stats it's only a look.
/// </remarks>
public static class SkinArmorSetBuilder
{
    /// <summary>
    /// The highest item number of an item group.
    /// </summary>
    public const short MaximumNumber = 511;

    /// <summary>
    /// The number of the Great Dragon set, whose size and slot the pieces take.
    /// </summary>
    private const short GreatDragonNumber = 21;

    /// <summary>
    /// Gets the item groups of the pieces: helms, armors, pants, gloves and boots.
    /// </summary>
    public static IReadOnlyList<byte> PieceGroups { get; } = [(byte)ItemGroups.Helm, (byte)ItemGroups.Armor, (byte)ItemGroups.Pants, (byte)ItemGroups.Gloves, (byte)ItemGroups.Boots];

    /// <summary>
    /// Gets the name of a piece of the set.
    /// </summary>
    /// <param name="setName">The name of the set, e.g. "Toranaga".</param>
    /// <param name="group">The item group of the piece.</param>
    /// <returns>The name of the piece, e.g. "Toranaga Helm".</returns>
    public static string GetPieceName(string setName, byte group) => (ItemGroups)group switch
    {
        ItemGroups.Helm => $"{setName} Helm",
        ItemGroups.Armor => $"{setName} Armor",
        ItemGroups.Pants => $"{setName} Pants",
        ItemGroups.Gloves => $"{setName} Gloves",
        ItemGroups.Boots => $"{setName} Boots",
        _ => throw new ArgumentOutOfRangeException(nameof(group), group, "The group isn't one of an armor piece."),
    };

    /// <summary>
    /// Adds the pieces which don't exist yet, and takes the stats off the ones which exist, e.g. from a version
    /// of the set which was a real armor.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    /// <param name="number">The item number of the pieces.</param>
    /// <param name="setName">The name of the set.</param>
    /// <returns>All pieces, ordered by group.</returns>
    public static IReadOnlyList<ItemDefinition> Build(IContext context, GameConfiguration gameConfiguration, short number, string setName)
    {
        var pieces = new List<ItemDefinition>();
        foreach (var group in PieceGroups)
        {
            if (FindItem(gameConfiguration, group, number) is { } existing)
            {
                MakeSkinOnly(existing);
                pieces.Add(existing);
                continue;
            }

            var greatDragon = FindItem(gameConfiguration, group, GreatDragonNumber)
                ?? throw new InvalidOperationException($"The Great Dragon piece of the group {group} doesn't exist.");
            pieces.Add(CreatePiece(context, gameConfiguration, greatDragon, number, GetPieceName(setName, group)));
        }

        return pieces;
    }

    /// <summary>
    /// Takes the stats off a piece: its defense, options and set bonuses; and stops it from dropping from monsters.
    /// </summary>
    /// <param name="piece">The piece.</param>
    /// <returns><c>true</c>, if something was changed.</returns>
    public static bool MakeSkinOnly(ItemDefinition piece)
    {
        var changed = piece.DropsFromMonsters
                      || piece.BasePowerUpAttributes.Count > 0
                      || piece.PossibleItemOptions.Count > 0
                      || piece.PossibleItemSetGroups.Count > 0;
        piece.DropsFromMonsters = false;
        piece.BasePowerUpAttributes.Clear();
        piece.PossibleItemOptions.Clear();
        piece.PossibleItemSetGroups.Clear();
        return changed;
    }

    /// <summary>
    /// Checks whether a piece is only a skin, see <see cref="MakeSkinOnly"/>.
    /// </summary>
    /// <param name="piece">The piece.</param>
    /// <returns><c>true</c>, if the piece has no stats and doesn't drop from monsters.</returns>
    public static bool IsSkinOnly(ItemDefinition piece) =>
        !piece.DropsFromMonsters
        && piece.BasePowerUpAttributes.Count == 0
        && piece.PossibleItemOptions.Count == 0
        && piece.PossibleItemSetGroups.Count == 0;

    /// <summary>
    /// Removes the set groups of the whole set from the configuration, e.g. the set bonuses of a version of the
    /// set which was a real armor.
    /// </summary>
    /// <param name="gameConfiguration">The game configuration.</param>
    /// <param name="number">The item number of the pieces.</param>
    /// <returns>The removed set groups.</returns>
    public static IReadOnlyList<ItemSetGroup> RemoveSetGroups(GameConfiguration gameConfiguration, short number)
    {
        var setGroups = FindSetGroups(gameConfiguration, number);
        foreach (var setGroup in setGroups)
        {
            gameConfiguration.ItemSetGroups.Remove(setGroup);
        }

        return setGroups;
    }

    /// <summary>
    /// Finds the set groups of the whole set: those whose items are all pieces of the set.
    /// </summary>
    /// <param name="gameConfiguration">The game configuration.</param>
    /// <param name="number">The item number of the pieces.</param>
    /// <returns>The set groups.</returns>
    public static IReadOnlyList<ItemSetGroup> FindSetGroups(GameConfiguration gameConfiguration, short number) =>
        gameConfiguration.ItemSetGroups
            .Where(g => g.Items.Count > 0 && g.Items.All(i => i.ItemDefinition is { } item && item.Number == number && PieceGroups.Contains(item.Group)))
            .ToList();

    /// <summary>
    /// Finds an item definition.
    /// </summary>
    /// <param name="gameConfiguration">The game configuration.</param>
    /// <param name="group">The item group.</param>
    /// <param name="number">The item number.</param>
    /// <returns>The item definition, if it exists.</returns>
    public static ItemDefinition? FindItem(GameConfiguration gameConfiguration, byte group, short number) =>
        gameConfiguration.Items.FirstOrDefault(i => i.Group == group && i.Number == number);

    private static bool IsRageFighter(CharacterClass characterClass) =>
        characterClass.Number is (byte)CharacterClassNumber.RageFighter or (byte)CharacterClassNumber.FistMaster;

    private static ItemDefinition CreatePiece(IContext context, GameConfiguration gameConfiguration, ItemDefinition greatDragon, short number, string name)
    {
        var piece = context.CreateNew<ItemDefinition>();
        piece.Group = greatDragon.Group;
        piece.Number = number;
        piece.Name = name;
        piece.Width = greatDragon.Width;
        piece.Height = greatDragon.Height;
        piece.MaximumItemLevel = greatDragon.MaximumItemLevel;
        piece.DropsFromMonsters = false;
        piece.Durability = greatDragon.Durability;
        piece.ItemSlot = greatDragon.ItemSlot;
        piece.SetGuid(piece.Group, piece.Number);

        // The cosmetic pieces can only be worn by the classes of the item. Like all gloves, the gloves can't be worn by the rage fighter.
        foreach (var characterClass in gameConfiguration.CharacterClasses.Where(c => piece.Group != (byte)ItemGroups.Gloves || !IsRageFighter(c)))
        {
            piece.QualifiedCharacters.Add(characterClass);
        }

        gameConfiguration.Items.Add(piece);
        return piece;
    }
}
