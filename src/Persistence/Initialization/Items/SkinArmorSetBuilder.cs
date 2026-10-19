// <copyright file="SkinArmorSetBuilder.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Items;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.Persistence.Initialization.CharacterClasses;

/// <summary>
/// Builds the armor sets of skin packs: a helm, armor, pants, gloves and boots with the same item number,
/// which every class can wear. The pieces take the size, options, defense and set bonuses of the Great Dragon
/// set, without requirements; they don't drop from monsters.
/// </summary>
/// <remarks>
/// The set is built in three steps, so that a running game server can take it over through the configuration
/// change listener without a restart: each step is saved on its own, and only references what the steps before
/// already published. First the pieces, then the set groups (which reference the pieces) and at last the links
/// from the pieces to their set groups. Each step only adds what's missing, so a set which was interrupted
/// can be completed by building it again.
/// </remarks>
public static class SkinArmorSetBuilder
{
    /// <summary>
    /// The highest item number of an item group.
    /// </summary>
    public const short MaximumNumber = 511;

    private const short GreatDragonNumber = 21;

    /// <summary>
    /// Gets the item groups of the pieces: helms, armors, pants, gloves and boots.
    /// </summary>
    public static IReadOnlyList<byte> PieceGroups { get; } = [(byte)ItemGroups.Helm, (byte)ItemGroups.Armor, (byte)ItemGroups.Pants, (byte)ItemGroups.Gloves, (byte)ItemGroups.Boots];

    /// <summary>
    /// Gets the name of a piece of the set.
    /// </summary>
    /// <param name="setName">The name of the set, e.g. "Pink Gold".</param>
    /// <param name="group">The item group of the piece.</param>
    /// <returns>The name of the piece, e.g. "Pink Gold Helm".</returns>
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
    /// Builds the whole set in one go, e.g. for a configuration which isn't used by a running game server.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    /// <param name="number">The item number of the pieces.</param>
    /// <param name="setName">The name of the set.</param>
    /// <returns>The pieces, ordered by group.</returns>
    public static IReadOnlyList<ItemDefinition> Build(IContext context, GameConfiguration gameConfiguration, short number, string setName)
    {
        var pieces = AddMissingPieces(context, gameConfiguration, number, setName);
        var setGroups = AddMissingSetGroups(context, gameConfiguration, pieces);
        LinkSetGroups(pieces, setGroups);
        return pieces;
    }

    /// <summary>
    /// Step 1: adds the pieces which don't exist yet.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    /// <param name="number">The item number of the pieces.</param>
    /// <param name="setName">The name of the set.</param>
    /// <returns>All pieces, ordered by group.</returns>
    public static IReadOnlyList<ItemDefinition> AddMissingPieces(IContext context, GameConfiguration gameConfiguration, short number, string setName)
    {
        var pieces = new List<ItemDefinition>();
        foreach (var group in PieceGroups)
        {
            if (FindItem(gameConfiguration, group, number) is { } existing)
            {
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
    /// Step 2: adds the set groups of the whole set, if they don't exist yet: the defense rate bonus and the defense
    /// bonus of each set level, like the Great Dragon set. The pieces aren't linked to them yet, see <see cref="LinkSetGroups"/>.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    /// <param name="pieces">The pieces of the set.</param>
    /// <returns>The set groups of the whole set.</returns>
    public static IReadOnlyList<ItemSetGroup> AddMissingSetGroups(IContext context, GameConfiguration gameConfiguration, IReadOnlyList<ItemDefinition> pieces)
    {
        var number = pieces[0].Number;
        var existing = FindSetGroups(gameConfiguration, number);
        if (existing.Count > 0)
        {
            return existing;
        }

        // Like for the sets of a new database, the set groups are named after the first word of the piece names.
        var setName = (pieces[0].Name.Value ?? string.Empty).Split(' ')[0];
        var greatDragonSetGroups = FindSetGroups(gameConfiguration, GreatDragonNumber);
        var result = new List<ItemSetGroup>();
        foreach (var greatDragonSetGroup in greatDragonSetGroups)
        {
            var setGroup = context.CreateNew<ItemSetGroup>();
            setGroup.Name = (greatDragonSetGroup.Name.Value ?? string.Empty).Replace("Great", setName, StringComparison.Ordinal);
            setGroup.MinimumItemCount = pieces.Count;
            setGroup.Options = greatDragonSetGroup.Options;
            setGroup.AlwaysApplies = greatDragonSetGroup.AlwaysApplies;
            setGroup.SetLevel = greatDragonSetGroup.SetLevel;

            var subNumber = greatDragonSetGroup.SetLevel > 0 ? (byte)greatDragonSetGroup.SetLevel : (byte)0xFF;
            foreach (var piece in pieces)
            {
                var itemOfSet = context.CreateNew<ItemOfItemSet>();
                itemOfSet.SetGuid(piece.Group, piece.Number, subNumber);
                itemOfSet.ItemDefinition = piece;
                setGroup.Items.Add(itemOfSet);
            }

            gameConfiguration.ItemSetGroups.Add(setGroup);
            result.Add(setGroup);
        }

        return result;
    }

    /// <summary>
    /// Step 3: links the pieces to the set groups of the whole set, where they aren't linked yet.
    /// </summary>
    /// <param name="pieces">The pieces of the set.</param>
    /// <param name="setGroups">The set groups of the whole set.</param>
    /// <returns><c>true</c>, if a link was added.</returns>
    public static bool LinkSetGroups(IReadOnlyList<ItemDefinition> pieces, IReadOnlyList<ItemSetGroup> setGroups)
    {
        var changed = false;
        foreach (var piece in pieces)
        {
            foreach (var setGroup in setGroups.Where(g => !piece.PossibleItemSetGroups.Contains(g)))
            {
                piece.PossibleItemSetGroups.Add(setGroup);
                changed = true;
            }
        }

        return changed;
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
        piece.DropLevel = greatDragon.DropLevel;
        piece.MaximumItemLevel = greatDragon.MaximumItemLevel;
        piece.DropsFromMonsters = false;
        piece.Durability = greatDragon.Durability;
        piece.ItemSlot = greatDragon.ItemSlot;
        piece.SetGuid(piece.Group, piece.Number);

        foreach (var option in greatDragon.PossibleItemOptions)
        {
            piece.PossibleItemOptions.Add(option);
        }

        foreach (var greatDragonPowerUp in greatDragon.BasePowerUpAttributes)
        {
            var powerUp = context.CreateNew<ItemBasePowerUpDefinition>();
            powerUp.TargetAttribute = greatDragonPowerUp.TargetAttribute;
            powerUp.BaseValue = greatDragonPowerUp.BaseValue;
            powerUp.AggregateType = greatDragonPowerUp.AggregateType;
            powerUp.BonusPerLevelTable = greatDragonPowerUp.BonusPerLevelTable;
            piece.BasePowerUpAttributes.Add(powerUp);
        }

        // Like all gloves, the gloves can't be worn by the rage fighter.
        foreach (var characterClass in gameConfiguration.CharacterClasses.Where(c => piece.Group != (byte)ItemGroups.Gloves || !IsRageFighter(c)))
        {
            piece.QualifiedCharacters.Add(characterClass);
        }

        gameConfiguration.Items.Add(piece);
        return piece;
    }
}
