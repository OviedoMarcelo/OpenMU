// <copyright file="AddQueenSocketSetPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Persistence.Initialization.CharacterClasses;
using MUnique.OpenMU.Persistence.Initialization.Items;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Adds the Queen set, the socket armor set of the Dimension Master (item number 53 of the helms, armors, pants, gloves and boots).
/// The pieces take the item slot, options, sockets and set bonuses of the Hades set, with their own defense and requirements.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("5B0C6E2A-8F41-4D7E-9A3C-2E6B1F94D7A8")]
public class AddQueenSocketSetPlugIn : UpdatePlugInBase
{
    /// <summary>
    /// The plug-in name.
    /// </summary>
    internal const string PlugInName = "Add the Queen socket set";

    /// <summary>
    /// The plug-in description.
    /// </summary>
    internal const string PlugInDescription = "Adds the Queen set, the socket armor set of the Dimension Master.";

    private const short QueenNumber = 53;

    private const short HadesNumber = 52;

    private const int LevelRequirement = 400;

    private const int StrengthRequirement = 175;

    private const int AgilityRequirement = 85;

    /// <summary>
    /// The pieces of the set: item group, name and defense.
    /// </summary>
    private static readonly (ItemGroups Group, string Name, int Defense)[] Pieces =
    [
        (ItemGroups.Helm, "Queen Helm", 44),
        (ItemGroups.Armor, "Queen Armor", 60),
        (ItemGroups.Pants, "Queen Pants", 50),
        (ItemGroups.Gloves, "Queen Gloves", 39),
        (ItemGroups.Boots, "Queen Boots", 42),
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
    public override DateTime CreatedAt => new(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        var dimensionMaster = gameConfiguration.CharacterClasses.FirstOrDefault(c => c.Number == (byte)CharacterClassNumber.DimensionMaster);
        var created = new List<(ItemDefinition Queen, ItemDefinition Hades)>();
        foreach (var (group, name, defense) in Pieces)
        {
            if (gameConfiguration.Items.Any(i => i.Group == (byte)group && i.Number == QueenNumber))
            {
                continue;
            }

            if (gameConfiguration.Items.FirstOrDefault(i => i.Group == (byte)group && i.Number == HadesNumber) is not { } hades)
            {
                continue;
            }

            var queen = CreatePiece(context, gameConfiguration, hades, name, defense);
            if (dimensionMaster is not null)
            {
                queen.QualifiedCharacters.Add(dimensionMaster);
            }

            created.Add((queen, hades));
        }

        if (created.Count > 0)
        {
            AddSetGroups(context, gameConfiguration, created);
        }

        return ValueTask.CompletedTask;
    }

    private static void AddSetGroups(IContext context, GameConfiguration gameConfiguration, List<(ItemDefinition Queen, ItemDefinition Hades)> pieces)
    {
        // The set groups of the whole Hades set: the defense rate bonus and the defense bonus of each set level.
        var hadesSetGroups = pieces[0].Hades.PossibleItemSetGroups
            .Where(g => g.Items.All(i => i.ItemDefinition?.Number == HadesNumber))
            .ToList();
        foreach (var hadesSetGroup in hadesSetGroups)
        {
            var setGroup = context.CreateNew<ItemSetGroup>();
            setGroup.Name = (hadesSetGroup.Name.Value ?? string.Empty).Replace("Hades", "Queen", StringComparison.Ordinal);
            setGroup.MinimumItemCount = pieces.Count;
            setGroup.Options = hadesSetGroup.Options;
            setGroup.AlwaysApplies = hadesSetGroup.AlwaysApplies;
            setGroup.SetLevel = hadesSetGroup.SetLevel;
            gameConfiguration.ItemSetGroups.Add(setGroup);

            var subNumber = hadesSetGroup.SetLevel > 0 ? (byte)hadesSetGroup.SetLevel : (byte)0xFF;
            foreach (var (queen, _) in pieces)
            {
                var itemOfSet = context.CreateNew<ItemOfItemSet>();
                itemOfSet.SetGuid(queen.Group, queen.Number, subNumber);
                itemOfSet.ItemDefinition = queen;
                setGroup.Items.Add(itemOfSet);
                queen.PossibleItemSetGroups.Add(setGroup);
            }
        }
    }

    private static ItemDefinition CreatePiece(IContext context, GameConfiguration gameConfiguration, ItemDefinition hades, string name, int defense)
    {
        var queen = context.CreateNew<ItemDefinition>();
        queen.Group = hades.Group;
        queen.Number = QueenNumber;
        queen.Name = name;
        queen.Width = hades.Width;
        queen.Height = hades.Height;
        queen.DropLevel = hades.DropLevel;
        queen.MaximumItemLevel = hades.MaximumItemLevel;
        queen.DropsFromMonsters = hades.DropsFromMonsters;
        queen.Durability = hades.Durability;
        queen.ItemSlot = hades.ItemSlot;
        queen.MaximumSockets = hades.MaximumSockets;
        queen.SetGuid(queen.Group, queen.Number);

        foreach (var option in hades.PossibleItemOptions)
        {
            queen.PossibleItemOptions.Add(option);
        }

        foreach (var hadesPowerUp in hades.BasePowerUpAttributes)
        {
            var powerUp = context.CreateNew<ItemBasePowerUpDefinition>();
            powerUp.TargetAttribute = hadesPowerUp.TargetAttribute;
            powerUp.BaseValue = hadesPowerUp.TargetAttribute?.Id == Stats.DefenseBase.Id ? defense : hadesPowerUp.BaseValue;
            powerUp.AggregateType = hadesPowerUp.AggregateType;
            powerUp.BonusPerLevelTable = hadesPowerUp.BonusPerLevelTable;
            queen.BasePowerUpAttributes.Add(powerUp);
        }

        AddRequirement(context, gameConfiguration, queen, Stats.Level, LevelRequirement);
        AddRequirement(context, gameConfiguration, queen, Stats.TotalStrengthRequirementValue, StrengthRequirement);
        AddRequirement(context, gameConfiguration, queen, Stats.TotalAgilityRequirementValue, AgilityRequirement);

        gameConfiguration.Items.Add(queen);
        return queen;
    }

    private static void AddRequirement(IContext context, GameConfiguration gameConfiguration, ItemDefinition item, AttributeDefinition attribute, int value)
    {
        var requirement = context.CreateNew<AttributeRequirement>();
        requirement.Attribute = attribute.GetPersistent(gameConfiguration);
        requirement.MinimumValue = value;
        item.Requirements.Add(requirement);
    }
}
