// <copyright file="AddSkinArmorSetPlugInBase.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.Persistence.Initialization.CharacterClasses;
using MUnique.OpenMU.Persistence.Initialization.Items;

/// <summary>
/// Base class of the updates which add an armor set of a skin pack (helm, armor, pants, gloves and boots), which every class can wear.
/// The pieces take the size, options, defense and set bonuses of the Great Dragon set, without requirements.
/// </summary>
public abstract class AddSkinArmorSetPlugInBase : UpdatePlugInBase
{
    private const short GreatDragonNumber = 21;

    /// <summary>
    /// The item groups of the pieces, in the order of <see cref="PieceNames"/>.
    /// </summary>
    private static readonly ItemGroups[] PieceGroups = [ItemGroups.Helm, ItemGroups.Armor, ItemGroups.Pants, ItemGroups.Gloves, ItemGroups.Boots];

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => false;

    /// <summary>
    /// Gets the item number of the pieces in their item groups.
    /// </summary>
    protected abstract short SetNumber { get; }

    /// <summary>
    /// Gets the names of the helm, armor, pants, gloves and boots.
    /// </summary>
    protected abstract string[] PieceNames { get; }

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        var created = new List<(ItemDefinition Piece, ItemDefinition GreatDragon)>();
        for (var i = 0; i < PieceGroups.Length; i++)
        {
            var group = (byte)PieceGroups[i];
            if (gameConfiguration.Items.Any(item => item.Group == group && item.Number == this.SetNumber))
            {
                continue;
            }

            if (gameConfiguration.Items.FirstOrDefault(item => item.Group == group && item.Number == GreatDragonNumber) is not { } greatDragon)
            {
                continue;
            }

            created.Add((this.CreatePiece(context, gameConfiguration, greatDragon, this.PieceNames[i]), greatDragon));
        }

        if (created.Count > 0)
        {
            this.AddSetGroups(context, gameConfiguration, created);
        }

        return ValueTask.CompletedTask;
    }

    private static bool IsRageFighter(CharacterClass characterClass) =>
        characterClass.Number is (byte)CharacterClassNumber.RageFighter or (byte)CharacterClassNumber.FistMaster;

    private void AddSetGroups(IContext context, GameConfiguration gameConfiguration, List<(ItemDefinition Piece, ItemDefinition GreatDragon)> pieces)
    {
        // Like for the sets of a new database, the set groups are named after the first word of the piece names.
        var setName = this.PieceNames[0].Split(' ')[0];

        // The set groups of the whole Great Dragon set: the defense rate bonus and the defense bonus of each set level.
        var greatDragonSetGroups = pieces[0].GreatDragon.PossibleItemSetGroups
            .Where(g => g.Items.All(i => i.ItemDefinition?.Number == GreatDragonNumber))
            .ToList();
        foreach (var greatDragonSetGroup in greatDragonSetGroups)
        {
            var setGroup = context.CreateNew<ItemSetGroup>();
            setGroup.Name = (greatDragonSetGroup.Name.Value ?? string.Empty).Replace("Great", setName, StringComparison.Ordinal);
            setGroup.MinimumItemCount = pieces.Count;
            setGroup.Options = greatDragonSetGroup.Options;
            setGroup.AlwaysApplies = greatDragonSetGroup.AlwaysApplies;
            setGroup.SetLevel = greatDragonSetGroup.SetLevel;
            gameConfiguration.ItemSetGroups.Add(setGroup);

            var subNumber = greatDragonSetGroup.SetLevel > 0 ? (byte)greatDragonSetGroup.SetLevel : (byte)0xFF;
            foreach (var (piece, _) in pieces)
            {
                var itemOfSet = context.CreateNew<ItemOfItemSet>();
                itemOfSet.SetGuid(piece.Group, piece.Number, subNumber);
                itemOfSet.ItemDefinition = piece;
                setGroup.Items.Add(itemOfSet);
                piece.PossibleItemSetGroups.Add(setGroup);
            }
        }
    }

    private ItemDefinition CreatePiece(IContext context, GameConfiguration gameConfiguration, ItemDefinition greatDragon, string name)
    {
        var piece = context.CreateNew<ItemDefinition>();
        piece.Group = greatDragon.Group;
        piece.Number = this.SetNumber;
        piece.Name = name;
        piece.Width = greatDragon.Width;
        piece.Height = greatDragon.Height;
        piece.DropLevel = greatDragon.DropLevel;
        piece.MaximumItemLevel = greatDragon.MaximumItemLevel;
        piece.DropsFromMonsters = greatDragon.DropsFromMonsters;
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
