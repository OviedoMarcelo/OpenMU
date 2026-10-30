// <copyright file="CustomMonstersInitialization.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Persistence.Initialization.Skills;

/// <summary>
/// Creates the custom monsters of MU Cyber, which the client shows with models of their own
/// (see docs/custom-monsters.md of the client). They are meant to be the bosses of encounters of
/// the boss encounters plugin: the encounter spawns them and sets their health and damage multipliers,
/// size, glow and drops. So they have no spawn and no drops of their own, and their stats are a base
/// like the ones of Kundun (level 140, 140000 health), which the "Kundun Reborn" encounter uses.
/// </summary>
internal class CustomMonstersInitialization : InitializerBase
{
    /// <summary>
    /// The number of the Coloso de Hielo. The original monsters go up to 668.
    /// </summary>
    internal const short IceColossusNumber = 700;

    /// <summary>
    /// The number of El Vigía.
    /// </summary>
    internal const short WatcherNumber = 701;

    /// <summary>
    /// The number of the Guardián del Baúl, an NPC which extends the vault (see VaultExtensionNpcPlugIn).
    /// The objects of the encounters take the numbers up to 707.
    /// </summary>
    internal const short VaultGuardianNumber = 708;

    /// <summary>
    /// The number of the Guardiana de Icarus, the strongest monster of Icarus.
    /// </summary>
    internal const short IcarusGuardianNumber = 709;

    /// <summary>
    /// The resets which a player needs, so that the Guardiana de Icarus drops its items.
    /// </summary>
    internal const short IcarusGuardianRequiredResets = 5;

    /// <summary>
    /// Where the Guardiana de Icarus spawns: in the zone of the Great Drakans, the strongest original monsters there.
    /// </summary>
    internal static readonly (byte X, byte Y)[] IcarusGuardianPositions = [(52, 174), (59, 190), (27, 220), (43, 238)];

    /// <summary>
    /// Where the Guardián del Baúl stands in Lorencia: next to Baz The Vault Keeper (147, 145).
    /// </summary>
    internal static readonly (byte X, byte Y) VaultGuardianPosition = (149, 144);

    /// <summary>
    /// The numbers and names of the objects of the boss encounters: crystals, pillars and obelisks in ice and fire.
    /// </summary>
    internal static readonly (short Number, string Name)[] EncounterObjects =
    [
        (702, "Cristal de Hielo"),
        (703, "Cristal de Fuego"),
        (704, "Pilar Rúnico de Hielo"),
        (705, "Pilar Rúnico de Fuego"),
        (706, "Obelisco de Hielo"),
        (707, "Obelisco de Fuego"),
    ];

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomMonstersInitialization" /> class.
    /// </summary>
    /// <param name="context">The persistence context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    public CustomMonstersInitialization(IContext context, GameConfiguration gameConfiguration)
        : base(context, gameConfiguration)
    {
    }

    /// <inheritdoc />
    public override void Initialize()
    {
        this.CreateIceColossus();
        this.CreateWatcher();
        this.CreateEncounterObjects();
        this.CreateVaultGuardian();
    }

    /// <summary>
    /// Creates the Coloso de Hielo, a giant of stone and ice of level 150, almost immune to ice.
    /// </summary>
    internal void CreateIceColossus()
    {
        this.CreateEncounterMonster(
            IceColossusNumber,
            "Coloso de Hielo",
            new Dictionary<AttributeDefinition, float>
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
            });
    }

    /// <summary>
    /// Creates El Vigía, a demon with a great sword of level 130, weaker than the Coloso de Hielo
    /// and resistant to fire.
    /// </summary>
    internal void CreateWatcher()
    {
        this.CreateEncounterMonster(
            WatcherNumber,
            "El Vigía",
            new Dictionary<AttributeDefinition, float>
            {
                { Stats.Level, 130 },
                { Stats.MaximumHealth, 120_000 },
                { Stats.MinimumPhysBaseDmg, 950 },
                { Stats.MaximumPhysBaseDmg, 1050 },
                { Stats.DefenseBase, 800 },
                { Stats.AttackRatePvm, 1000 },
                { Stats.DefenseRatePvm, 420 },
                { Stats.IceResistance, 30f / 255 },
                { Stats.PoisonResistance, 30f / 255 },
                { Stats.LightningResistance, 30f / 255 },
                { Stats.FireResistance, 120f / 255 },
            });
    }

    /// <summary>
    /// Creates the objects of the boss encounters, which the players have to break in a phase, like the
    /// pillars and shields. They don't move, don't attack and drop nothing; the phase sets their health.
    /// </summary>
    internal void CreateEncounterObjects()
    {
        foreach (var (number, name) in EncounterObjects)
        {
            if (this.GameConfiguration.Monsters.Any(m => m.Number == number))
            {
                continue;
            }

            var monster = this.Context.CreateNew<MonsterDefinition>();
            this.GameConfiguration.Monsters.Add(monster);
            monster.Number = number;
            monster.Designation = name;
            monster.MoveRange = 0;
            monster.AttackRange = 0;
            monster.ViewRange = 0;
            monster.MoveDelay = new TimeSpan(500 * TimeSpan.TicksPerMillisecond);
            monster.AttackDelay = new TimeSpan(2000 * TimeSpan.TicksPerMillisecond);
            monster.RespawnDelay = new TimeSpan(600 * TimeSpan.TicksPerSecond);
            monster.NumberOfMaximumItemDrops = 0;
            var attributes = new Dictionary<AttributeDefinition, float>
            {
                { Stats.Level, 100 },
                { Stats.MaximumHealth, 30_000 },
                { Stats.DefenseBase, 600 },
                { Stats.DefenseRatePvm, 400 },
            };
            monster.AddAttributes(attributes, this.Context, this.GameConfiguration);
            monster.SetGuid(monster.Number);
        }
    }

    /// <summary>
    /// Creates the Guardián del Baúl, an NPC which extends the vault of the account in exchange for some items.
    /// </summary>
    /// <returns>The NPC.</returns>
    internal MonsterDefinition CreateVaultGuardian()
    {
        if (this.GameConfiguration.Monsters.FirstOrDefault(m => m.Number == VaultGuardianNumber) is { } existing)
        {
            return existing;
        }

        var npc = this.Context.CreateNew<MonsterDefinition>();
        this.GameConfiguration.Monsters.Add(npc);
        npc.Number = VaultGuardianNumber;
        npc.Designation = "Guardián del Baúl";
        npc.ObjectKind = NpcObjectKind.PassiveNpc;
        npc.NpcWindow = NpcWindow.Undefined;
        npc.SetGuid(npc.Number);
        return npc;
    }

    /// <summary>
    /// Places the Guardián del Baúl next to the vault keeper of Lorencia, unless it's already there.
    /// </summary>
    /// <param name="lorencia">The map of Lorencia.</param>
    /// <param name="npc">The Guardián del Baúl.</param>
    internal void CreateVaultGuardianSpawn(GameMapDefinition lorencia, MonsterDefinition npc)
    {
        if (lorencia.MonsterSpawns.Any(s => s.MonsterDefinition?.Number == VaultGuardianNumber))
        {
            return;
        }

        var spawn = this.Context.CreateNew<MonsterSpawnArea>();
        lorencia.MonsterSpawns.Add(spawn);
        spawn.SetGuid(lorencia.Number, VaultGuardianNumber);
        spawn.GameMap = lorencia;
        spawn.MonsterDefinition = npc;
        spawn.Quantity = 1;
        spawn.SpawnTrigger = SpawnTrigger.Automatic;
        spawn.Direction = Direction.SouthWest;
        spawn.X1 = spawn.X2 = VaultGuardianPosition.X;
        spawn.Y1 = spawn.Y2 = VaultGuardianPosition.Y;
    }

    /// <summary>
    /// Creates the Guardiana de Icarus, a monster of level 120 with stats like the ones of the Werewolf (level 118).
    /// It drops the Flame of Condor for players with enough resets, see <see cref="IcarusGuardianRequiredResets"/>.
    /// </summary>
    /// <returns>The monster.</returns>
    internal MonsterDefinition CreateIcarusGuardian()
    {
        if (this.GameConfiguration.Monsters.FirstOrDefault(m => m.Number == IcarusGuardianNumber) is { } existing)
        {
            return existing;
        }

        var monster = this.Context.CreateNew<MonsterDefinition>();
        this.GameConfiguration.Monsters.Add(monster);
        monster.Number = IcarusGuardianNumber;
        monster.Designation = "Guardiana de Icarus";
        monster.MoveRange = 3;
        monster.AttackRange = 2;
        monster.ViewRange = 7;
        monster.MoveDelay = new TimeSpan(500 * TimeSpan.TicksPerMillisecond);
        monster.AttackDelay = new TimeSpan(1500 * TimeSpan.TicksPerMillisecond);
        monster.RespawnDelay = new TimeSpan(60 * TimeSpan.TicksPerSecond);
        monster.Attribute = 2;
        monster.NumberOfMaximumItemDrops = 1;
        var attributes = new Dictionary<AttributeDefinition, float>
        {
            { Stats.Level, 120 },
            { Stats.MaximumHealth, 115_000 },
            { Stats.MinimumPhysBaseDmg, 850 },
            { Stats.MaximumPhysBaseDmg, 880 },
            { Stats.DefenseBase, 700 },
            { Stats.AttackRatePvm, 980 },
            { Stats.DefenseRatePvm, 370 },
            { Stats.PoisonResistance, 20f / 255 },
            { Stats.IceResistance, 20f / 255 },
            { Stats.LightningResistance, 20f / 255 },
            { Stats.FireResistance, 20f / 255 },
            { Stats.Resets, IcarusGuardianRequiredResets },
        };
        monster.AddAttributes(attributes, this.Context, this.GameConfiguration);
        monster.SetGuid(monster.Number);

        var flameOfCondor = this.Context.CreateNew<DropItemGroup>();
        flameOfCondor.SetGuid(IcarusGuardianNumber, 1);
        flameOfCondor.Chance = 0.005;
        flameOfCondor.Description = "Flame of Condor (Guardiana de Icarus)";
        flameOfCondor.Monster = monster;
        flameOfCondor.PossibleItems.Add(this.GameConfiguration.Items.First(item => item.Group == 13 && item.Number == 52));
        monster.DropItemGroups.Add(flameOfCondor);
        this.GameConfiguration.DropItemGroups.Add(flameOfCondor);
        return monster;
    }

    /// <summary>
    /// Places the Guardiana de Icarus on its spawns, unless they're already there.
    /// </summary>
    /// <param name="icarus">The map of Icarus.</param>
    /// <param name="monster">The Guardiana de Icarus.</param>
    internal void CreateIcarusGuardianSpawns(GameMapDefinition icarus, MonsterDefinition monster)
    {
        if (icarus.MonsterSpawns.Any(s => s.MonsterDefinition?.Number == IcarusGuardianNumber))
        {
            return;
        }

        for (short i = 0; i < IcarusGuardianPositions.Length; i++)
        {
            var (x, y) = IcarusGuardianPositions[i];
            var spawn = this.Context.CreateNew<MonsterSpawnArea>();
            icarus.MonsterSpawns.Add(spawn);
            spawn.SetGuid(icarus.Number, (short)(IcarusGuardianNumber + i));
            spawn.GameMap = icarus;
            spawn.MonsterDefinition = monster;
            spawn.Quantity = 1;
            spawn.SpawnTrigger = SpawnTrigger.Automatic;
            spawn.Direction = Direction.Undefined;
            spawn.X1 = spawn.X2 = x;
            spawn.Y1 = spawn.Y2 = y;
        }
    }

    private void CreateEncounterMonster(short number, string name, Dictionary<AttributeDefinition, float> attributes)
    {
        if (this.GameConfiguration.Monsters.Any(m => m.Number == number))
        {
            return;
        }

        var monster = this.Context.CreateNew<MonsterDefinition>();
        this.GameConfiguration.Monsters.Add(monster);
        monster.Number = number;
        monster.Designation = name;
        monster.MoveRange = 3;
        monster.AttackRange = 3;
        monster.ViewRange = 7;
        monster.MoveDelay = new TimeSpan(500 * TimeSpan.TicksPerMillisecond);
        monster.AttackDelay = new TimeSpan(1600 * TimeSpan.TicksPerMillisecond);

        // Only matters when it's placed as a normal spawn; an encounter spawns it by itself.
        monster.RespawnDelay = new TimeSpan(600 * TimeSpan.TicksPerSecond);
        monster.Attribute = 2;
        monster.NumberOfMaximumItemDrops = 1;
        monster.AttackSkill = this.GameConfiguration.Skills.FirstOrDefault(s => s.Number == (short)SkillNumber.MonsterSkill);
        monster.AddAttributes(attributes, this.Context, this.GameConfiguration);
        monster.SetGuid(monster.Number);
    }
}
