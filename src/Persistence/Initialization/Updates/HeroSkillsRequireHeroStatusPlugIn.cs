// <copyright file="HeroSkillsRequireHeroStatusPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// As in the original Season 6, the Scroll of Nova, the Scroll of Wizardry Enhance and the Crystals of
/// Multi-Shot and Recovery can only be read after Marlon's 'Gain Hero Status' quest, like the Crystal of
/// Destruction already was. The game client enforces this too, so without it both sides disagreed.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("6F1C3A52-8D47-4E0B-9B6A-2C5E7D91A3F4")]
public class HeroSkillsRequireHeroStatusPlugIn : UpdatePlugInBase
{
    /// <summary>
    /// The plug in name.
    /// </summary>
    internal const string PlugInName = "Hero skills require the Hero Status quest";

    /// <summary>
    /// The plug in description.
    /// </summary>
    internal const string PlugInDescription = "The Scroll of Nova, the Scroll of Wizardry Enhance and the Crystals of Multi-Shot and Recovery require Marlon's 'Gain Hero Status' quest, as in the original game.";

    private static readonly (byte Group, short Number)[] HeroSkillItems =
    [
        (15, 18), // Scroll of Nova
        (15, 28), // Scroll of Wizardry Enhance
        (12, 45), // Crystal of Multi-Shot
        (12, 46), // Crystal of Recovery
    ];

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 09, 30, 12, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        var heroStatus = Stats.GainHeroStatusQuestCompleted.GetPersistent(gameConfiguration);
        foreach (var item in gameConfiguration.Items.Where(i => HeroSkillItems.Contains((i.Group, i.Number))))
        {
            if (item.Requirements.Any(r => r.Attribute == heroStatus))
            {
                continue;
            }

            var requirement = context.CreateNew<AttributeRequirement>();
            requirement.Attribute = heroStatus;
            requirement.MinimumValue = 1;
            item.Requirements.Add(requirement);
        }

        return ValueTask.CompletedTask;
    }
}
