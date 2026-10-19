// <copyright file="AddPinkGoldSetPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.Persistence.Initialization.CharacterClasses;
using MUnique.OpenMU.Persistence.Initialization.Items;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Adds the Pink Gold set (item number 54 of the helms, armors, pants, gloves and boots), which every class can wear.
/// The pieces take the size, options, defense and set bonuses of the Great Dragon set, without requirements.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("80A4B5D1-D5D3-41CD-81F6-6FD71085889A")]
public class AddPinkGoldSetPlugIn : UpdatePlugInBase
{
    /// <summary>
    /// The plug-in name.
    /// </summary>
    internal const string PlugInName = "Add the Pink Gold set";

    /// <summary>
    /// The plug-in description.
    /// </summary>
    internal const string PlugInDescription = "Adds the Pink Gold set, an armor set which every class can wear.";

    private const short PinkGoldNumber = 54;

    private const short GreatDragonNumber = 21;

    /// <summary>
    /// The pieces of the set: item group and name.
    /// </summary>
    private static readonly (ItemGroups Group, string Name)[] Pieces =
    [
        (ItemGroups.Helm, "Pink Gold Helm"),
        (ItemGroups.Armor, "Pink Gold Armor"),
        (ItemGroups.Pants, "Pink Gold Pants"),
        (ItemGroups.Gloves, "Pink Gold Gloves"),
        (ItemGroups.Boots, "Pink Gold Boots"),
    ];

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => false;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        var created = new List<(ItemDefinition PinkGold, ItemDefinition GreatDragon)>();
        foreach (var (group, name) in Pieces)
        {
            if (gameConfiguration.Items.Any(i => i.Group == (byte)group && i.Number == PinkGoldNumber))
            {
                continue;
            }

            if (gameConfiguration.Items.FirstOrDefault(i => i.Group == (byte)group && i.Number == GreatDragonNumber) is not { } greatDragon)
            {
                continue;
            }

            created.Add((CreatePiece(context, gameConfiguration, greatDragon, name), greatDragon));
        }

        if (created.Count > 0)
        {
            AddSetGroups(context, gameConfiguration, created);
        }

        return ValueTask.CompletedTask;
    }

    private static void AddSetGroups(IContext context, GameConfiguration gameConfiguration, List<(ItemDefinition PinkGold, ItemDefinition GreatDragon)> pieces)
    {
        // The set groups of the whole Great Dragon set: the defense rate bonus and the defense bonus of each set level.
        var greatDragonSetGroups = pieces[0].GreatDragon.PossibleItemSetGroups
            .Where(g => g.Items.All(i => i.ItemDefinition?.Number == GreatDragonNumber))
            .ToList();
        foreach (var greatDragonSetGroup in greatDragonSetGroups)
        {
            var setGroup = context.CreateNew<ItemSetGroup>();
            setGroup.Name = (greatDragonSetGroup.Name.Value ?? string.Empty).Replace("Great", "Pink", StringComparison.Ordinal);
            setGroup.MinimumItemCount = pieces.Count;
            setGroup.Options = greatDragonSetGroup.Options;
            setGroup.AlwaysApplies = greatDragonSetGroup.AlwaysApplies;
            setGroup.SetLevel = greatDragonSetGroup.SetLevel;
            gameConfiguration.ItemSetGroups.Add(setGroup);

            var subNumber = greatDragonSetGroup.SetLevel > 0 ? (byte)greatDragonSetGroup.SetLevel : (byte)0xFF;
            foreach (var (pinkGold, _) in pieces)
            {
                var itemOfSet = context.CreateNew<ItemOfItemSet>();
                itemOfSet.SetGuid(pinkGold.Group, pinkGold.Number, subNumber);
                itemOfSet.ItemDefinition = pinkGold;
                setGroup.Items.Add(itemOfSet);
                pinkGold.PossibleItemSetGroups.Add(setGroup);
            }
        }
    }

    private static ItemDefinition CreatePiece(IContext context, GameConfiguration gameConfiguration, ItemDefinition greatDragon, string name)
    {
        var pinkGold = context.CreateNew<ItemDefinition>();
        pinkGold.Group = greatDragon.Group;
        pinkGold.Number = PinkGoldNumber;
        pinkGold.Name = name;
        pinkGold.Width = greatDragon.Width;
        pinkGold.Height = greatDragon.Height;
        pinkGold.DropLevel = greatDragon.DropLevel;
        pinkGold.MaximumItemLevel = greatDragon.MaximumItemLevel;
        pinkGold.DropsFromMonsters = greatDragon.DropsFromMonsters;
        pinkGold.Durability = greatDragon.Durability;
        pinkGold.ItemSlot = greatDragon.ItemSlot;
        pinkGold.SetGuid(pinkGold.Group, pinkGold.Number);

        foreach (var option in greatDragon.PossibleItemOptions)
        {
            pinkGold.PossibleItemOptions.Add(option);
        }

        foreach (var greatDragonPowerUp in greatDragon.BasePowerUpAttributes)
        {
            var powerUp = context.CreateNew<ItemBasePowerUpDefinition>();
            powerUp.TargetAttribute = greatDragonPowerUp.TargetAttribute;
            powerUp.BaseValue = greatDragonPowerUp.BaseValue;
            powerUp.AggregateType = greatDragonPowerUp.AggregateType;
            powerUp.BonusPerLevelTable = greatDragonPowerUp.BonusPerLevelTable;
            pinkGold.BasePowerUpAttributes.Add(powerUp);
        }

        // Like all gloves, the gloves can't be worn by the rage fighter.
        foreach (var characterClass in gameConfiguration.CharacterClasses.Where(c => pinkGold.Group != (byte)ItemGroups.Gloves || !IsRageFighter(c)))
        {
            pinkGold.QualifiedCharacters.Add(characterClass);
        }

        gameConfiguration.Items.Add(pinkGold);
        return pinkGold;
    }

    private static bool IsRageFighter(CharacterClass characterClass) =>
        characterClass.Number is (byte)CharacterClassNumber.RageFighter or (byte)CharacterClassNumber.FistMaster;
}
