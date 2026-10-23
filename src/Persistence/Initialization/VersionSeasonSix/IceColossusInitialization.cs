// <copyright file="IceColossusInitialization.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Persistence.Initialization.Skills;

/// <summary>
/// Creates the Ice Colossus ("Coloso de Hielo"), a custom monster of MU Cyber which is meant to be
/// the boss of an encounter of the boss encounters plugin: the encounter spawns it and sets its health
/// and damage multipliers, size, glow and drops. So it has no spawn and no drops of its own, and its
/// stats are a base like the ones of Kundun (level 140), which the "Kundun Reborn" encounter uses.
/// The client shows it with its own model (Data/Monster/Monster218.bmd).
/// </summary>
internal class IceColossusInitialization : InitializerBase
{
    /// <summary>
    /// The number of the monster. The original monsters go up to 668.
    /// </summary>
    internal const short Number = 700;

    /// <summary>
    /// Initializes a new instance of the <see cref="IceColossusInitialization" /> class.
    /// </summary>
    /// <param name="context">The persistence context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    public IceColossusInitialization(IContext context, GameConfiguration gameConfiguration)
        : base(context, gameConfiguration)
    {
    }

    /// <inheritdoc />
    public override void Initialize()
    {
        if (this.GameConfiguration.Monsters.Any(m => m.Number == Number))
        {
            return;
        }

        var monster = this.Context.CreateNew<MonsterDefinition>();
        this.GameConfiguration.Monsters.Add(monster);
        monster.Number = Number;
        monster.Designation = "Coloso de Hielo";
        monster.MoveRange = 3;
        monster.AttackRange = 3;
        monster.ViewRange = 7;
        monster.MoveDelay = new TimeSpan(500 * TimeSpan.TicksPerMillisecond);
        monster.AttackDelay = new TimeSpan(1600 * TimeSpan.TicksPerMillisecond);
        monster.RespawnDelay = new TimeSpan(600 * TimeSpan.TicksPerSecond);
        monster.Attribute = 2;
        monster.NumberOfMaximumItemDrops = 1;
        monster.AttackSkill = this.GameConfiguration.Skills.FirstOrDefault(s => s.Number == (short)SkillNumber.MonsterSkill);

        // A bit stronger than Kundun (level 140), and almost immune to ice.
        var attributes = new Dictionary<AttributeDefinition, float>
        {
            { Stats.Level, 150 },
            { Stats.MaximumHealth, 150_000 },
            { Stats.MinimumPhysBaseDmg, 1150 },
            { Stats.MaximumPhysBaseDmg, 1250 },
            { Stats.DefenseBase, 950 },
            { Stats.AttackRatePvm, 1150 },
            { Stats.DefenseRatePvm, 500 },
            { Stats.IceResistance, 254f / 255 },
            { Stats.PoisonResistance, 30f / 255 },
            { Stats.LightningResistance, 30f / 255 },
            { Stats.FireResistance, 30f / 255 },
        };
        monster.AddAttributes(attributes, this.Context, this.GameConfiguration);
        monster.SetGuid(monster.Number);
    }
}
