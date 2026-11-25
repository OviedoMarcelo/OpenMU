// <copyright file="ChristmasEventInitialization.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Attributes;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Persistence.Initialization.Items;
using MUnique.OpenMU.Persistence.Initialization.Skills;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Maps;

/// <summary>
/// The data of the Christmas event of MU Cyber: the Invitation to Santa Village, the gate where the players arrive in
/// Santa Village, Santa Claus, the buffs of the Little Santas, the Duende de Santa which sells the invitation in Lorencia,
/// the monsters of the invasion of Santa (Evil Goblin and Cursed Santa) and the prizes of the Christmas Star.
/// Everything is only added when it's missing, so that it can run on existing databases, too.
/// </summary>
internal class ChristmasEventInitialization : InitializerBase
{
    /// <summary>
    /// The number of the Duende de Santa, which sells the Invitation to Santa Village in Lorencia.
    /// The client of MU Cyber shows it with the model of a Little Santa.
    /// </summary>
    internal const short SantaElfNumber = 711;

    /// <summary>
    /// The number of Santa Claus.
    /// </summary>
    internal const short SantaClausNumber = 465;

    /// <summary>
    /// The number of the Evil Goblin.
    /// </summary>
    internal const short EvilGoblinNumber = 466;

    /// <summary>
    /// The number of the Cursed Santa.
    /// </summary>
    internal const short CursedSantaNumber = 476;

    /// <summary>
    /// Where the Duende de Santa stands: in the middle of the town of Lorencia.
    /// </summary>
    internal static readonly (byte X, byte Y) SantaElfPosition = (140, 120);

    /// <summary>
    /// Where Santa Claus stands: in the square of Santa Village, between the Little Santas.
    /// </summary>
    internal static readonly (byte X, byte Y) SantaClausPosition = (210, 25);

    /// <summary>
    /// Where the players arrive in Santa Village: in front of the Snowman (202, 41). The whole village is a safe zone.
    /// </summary>
    internal static readonly (byte X1, byte Y1, byte X2, byte Y2) ArrivalArea = (205, 36, 210, 39);

    private const byte InvitationGroup = 13;
    private const short InvitationNumber = 66;
    private const byte ChristmasStarGroup = 14;
    private const short ChristmasStarNumber = 51;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChristmasEventInitialization"/> class.
    /// </summary>
    /// <param name="context">The persistence context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    public ChristmasEventInitialization(IContext context, GameConfiguration gameConfiguration)
        : base(context, gameConfiguration)
    {
    }

    /// <inheritdoc />
    public override void Initialize()
    {
        var invitation = this.CreateInvitation();
        this.CreateSantaBuffs();
        this.CreateChristmasStarPrizes();
        var santaClaus = this.CreateSantaClaus();
        var elf = this.CreateSantaElf(invitation);
        var evilGoblin = this.CreateEvilGoblin();
        var cursedSanta = this.CreateCursedSanta();
        this.AddChristmasStarDrops(evilGoblin, cursedSanta);

        if (this.GameConfiguration.Maps.FirstOrDefault(m => m.Number == SantaVillage.Number) is { } santaVillage)
        {
            this.CreateArrivalGate(santaVillage);
            this.CreateSpawn(santaVillage, santaClaus, SantaClausPosition, Direction.South);
        }

        if (this.GameConfiguration.Maps.FirstOrDefault(m => m.Number == Lorencia.Number) is { } lorencia)
        {
            this.CreateSpawn(lorencia, elf, SantaElfPosition, Direction.South);
        }
    }

    /// <summary>
    /// Creates the Invitation to Santa Village (13, 66). It's used once and can't be dropped by monsters.
    /// </summary>
    /// <returns>The item definition.</returns>
    internal ItemDefinition CreateInvitation()
    {
        if (this.GameConfiguration.Items.FirstOrDefault(i => i.Group == InvitationGroup && i.Number == InvitationNumber) is { } existing)
        {
            return existing;
        }

        var item = this.Context.CreateNew<ItemDefinition>();
        this.GameConfiguration.Items.Add(item);
        item.Group = InvitationGroup;
        item.Number = InvitationNumber;
        item.Name = "Invitation to Santa Village";
        item.Width = 1;
        item.Height = 1;
        item.Durability = 1;
        item.DropsFromMonsters = false;
        item.MaximumItemLevel = 0;
        item.SetGuid(item.Group, item.Number);
        foreach (var characterClass in this.GameConfiguration.CharacterClasses)
        {
            item.QualifiedCharacters.Add(characterClass);
        }

        return item;
    }

    /// <summary>
    /// Creates the buffs of the Little Santas. Their numbers are the ones of the buffs of the client, which shows their texts.
    /// </summary>
    internal void CreateSantaBuffs()
    {
        this.CreateSantaBuff(92, "Cure of Santa", (Stats.MaximumHealth, 500, AggregateType.AddRaw));
        this.CreateSantaBuff(93, "Safeguard of Santa", (Stats.MaximumMana, 500, AggregateType.AddRaw));
        this.CreateSantaBuff(94, "Strength of Santa", (Stats.BaseDamageBonus, 30, AggregateType.AddRaw));

        // Like the Jack O'Lantern Cry: the final defense is halved, so +50 shows +100 in the client.
        this.CreateSantaBuff(95, "Defense of Santa", (Stats.DefenseFinal, 50, AggregateType.AddFinal));
        this.CreateSantaBuff(96, "Quickness of Santa", (Stats.AttackSpeedAny, 15, AggregateType.AddRaw));
        this.CreateSantaBuff(97, "Luck of Santa", (Stats.AbilityRecoveryAbsolute, 10, AggregateType.AddRaw));
    }

    /// <summary>
    /// Adds the prizes of the Christmas Star (14, 51): small prizes at level 0 (which drops in the invasion of Santa)
    /// and the special ones at level 1 (the gift of Santa Claus). When nothing is won, it only shows the fireworks.
    /// </summary>
    internal void CreateChristmasStarPrizes()
    {
        if (this.GameConfiguration.Items.FirstOrDefault(i => i.Group == ChristmasStarGroup && i.Number == ChristmasStarNumber) is not { } star)
        {
            return;
        }

        star.MaximumItemLevel = Math.Max(star.MaximumItemLevel, (byte)1);
        foreach (var effect in star.DropItems.Where(d => d.ItemType == SpecialItemType.None))
        {
            effect.DropEffect = ItemDropEffect.ChristmasFireworks;
        }

        if (star.DropItems.Any(d => d.SourceItemLevel > 0 || d.ItemType != SpecialItemType.None))
        {
            // The prizes have already been added.
            return;
        }

        // Level 0: what drops in the invasion of Santa.
        this.AddPrize(star, 0, 0.10, "Christmas Star - Jewel of Bless", (14, 13, 0));
        this.AddPrize(star, 0, 0.10, "Christmas Star - Jewel of Soul", (14, 14, 0));
        this.AddPrize(star, 0, 0.05, "Christmas Star - Jewel of Life", (14, 16, 0));
        this.AddPrize(star, 0, 0.05, "Christmas Star - Jewel of Chaos", (12, 15, 0));
        this.AddPrize(star, 0, 0.02, "Christmas Star - Snowman Transformation Ring", (13, 68, 0));
        this.AddEffectOnly(star, 1);

        // Level 1: the gift of Santa Claus.
        this.AddPrize(star, 1, 0.15, "Christmas Star +1 - Pack of 10 Jewels of Bless", (12, 30, 0));
        this.AddPrize(star, 1, 0.15, "Christmas Star +1 - Pack of 10 Jewels of Soul", (12, 31, 0));
        this.AddPrize(star, 1, 0.10, "Christmas Star +1 - Pack of 10 Jewels of Life", (12, 136, 0));
        this.AddPrize(star, 1, 0.10, "Christmas Star +1 - Pack of 10 Jewels of Chaos", (12, 141, 0));
        this.AddPrize(star, 1, 0.10, "Christmas Star +1 - Pet Rudolf", (13, 67, 0));
        this.AddPrize(star, 1, 0.08, "Christmas Star +1 - Christmas Transformation Ring", (13, 41, 0));
        this.AddPrize(star, 1, 0.08, "Christmas Star +1 - Snowman Transformation Ring", (13, 68, 0));
        this.AddPrize(star, 1, 0.09, "Christmas Star +1 - Box of Kundun +3", (14, 11, 10));

        var ancient = this.Context.CreateNew<ItemDropItemGroup>();
        ancient.Description = "Christmas Star +1 - Ancient";
        ancient.ItemType = SpecialItemType.Ancient;
        ancient.SourceItemLevel = 1;
        ancient.Chance = 0.15;
        ancient.DropEffect = ItemDropEffect.ChristmasFireworks;
        star.DropItems.Add(ancient);

        this.AddEffectOnly(star, 1);
    }

    /// <summary>
    /// Creates Santa Claus, which gives the daily gift in Santa Village.
    /// </summary>
    /// <returns>The NPC.</returns>
    internal MonsterDefinition CreateSantaClaus()
    {
        if (this.GameConfiguration.Monsters.FirstOrDefault(m => m.Number == SantaClausNumber) is { } existing)
        {
            return existing;
        }

        var npc = this.Context.CreateNew<MonsterDefinition>();
        this.GameConfiguration.Monsters.Add(npc);
        npc.Number = SantaClausNumber;
        npc.Designation = "Santa Claus";
        npc.ObjectKind = NpcObjectKind.PassiveNpc;
        npc.NpcWindow = NpcWindow.Undefined;
        npc.SetGuid(npc.Number);
        return npc;
    }

    /// <summary>
    /// Creates the Duende de Santa, a merchant which sells the Invitation to Santa Village in Lorencia.
    /// </summary>
    /// <param name="invitation">The invitation.</param>
    /// <returns>The NPC.</returns>
    internal MonsterDefinition CreateSantaElf(ItemDefinition invitation)
    {
        if (this.GameConfiguration.Monsters.FirstOrDefault(m => m.Number == SantaElfNumber) is { } existing)
        {
            return existing;
        }

        var npc = this.Context.CreateNew<MonsterDefinition>();
        this.GameConfiguration.Monsters.Add(npc);
        npc.Number = SantaElfNumber;
        npc.Designation = "Duende de Santa";
        npc.ObjectKind = NpcObjectKind.PassiveNpc;
        npc.NpcWindow = NpcWindow.Merchant;
        npc.SetGuid(npc.Number);

        var store = this.Context.CreateNew<ItemStorage>();
        store.SetGuid(SantaElfNumber);
        var item = this.Context.CreateNew<Item>();
        item.Definition = invitation;
        item.ItemSlot = 0;
        item.Durability = invitation.Durability;
        store.Items.Add(item);
        npc.MerchantStore = store;
        return npc;
    }

    /// <summary>
    /// Creates the Evil Goblin, the monster of the invasion of Santa.
    /// </summary>
    /// <returns>The monster.</returns>
    internal MonsterDefinition CreateEvilGoblin()
    {
        if (this.GameConfiguration.Monsters.FirstOrDefault(m => m.Number == EvilGoblinNumber) is { } existing)
        {
            return existing;
        }

        var monster = this.Context.CreateNew<MonsterDefinition>();
        this.GameConfiguration.Monsters.Add(monster);
        monster.Number = EvilGoblinNumber;
        monster.Designation = "Evil Goblin";
        monster.SetGuid(monster.Number);
        this.SetUpInvasionMonster(
            monster,
            new Dictionary<AttributeDefinition, float>
            {
                { Stats.Level, 85 },
                { Stats.MaximumHealth, 25_000 },
                { Stats.MinimumPhysBaseDmg, 380 },
                { Stats.MaximumPhysBaseDmg, 420 },
                { Stats.DefenseBase, 260 },
                { Stats.AttackRatePvm, 600 },
                { Stats.DefenseRatePvm, 220 },
                { Stats.PoisonResistance, 10f / 255 },
                { Stats.IceResistance, 10f / 255 },
                { Stats.LightningResistance, 10f / 255 },
                { Stats.FireResistance, 10f / 255 },
            });
        return monster;
    }

    /// <summary>
    /// Creates the Cursed Santa, the boss of the invasion of Santa. In the original data it's a passive NPC, so it's
    /// turned into a monster.
    /// </summary>
    /// <returns>The monster.</returns>
    internal MonsterDefinition CreateCursedSanta()
    {
        var monster = this.GameConfiguration.Monsters.FirstOrDefault(m => m.Number == CursedSantaNumber);
        if (monster is null)
        {
            monster = this.Context.CreateNew<MonsterDefinition>();
            this.GameConfiguration.Monsters.Add(monster);
            monster.Number = CursedSantaNumber;
            monster.SetGuid(monster.Number);
        }
        else if (monster.ObjectKind == NpcObjectKind.Monster)
        {
            return monster;
        }

        monster.Designation = "Cursed Santa";
        this.SetUpInvasionMonster(
            monster,
            new Dictionary<AttributeDefinition, float>
            {
                { Stats.Level, 120 },
                { Stats.MaximumHealth, 250_000 },
                { Stats.MinimumPhysBaseDmg, 850 },
                { Stats.MaximumPhysBaseDmg, 950 },
                { Stats.DefenseBase, 600 },
                { Stats.AttackRatePvm, 1000 },
                { Stats.DefenseRatePvm, 380 },
                { Stats.PoisonResistance, 30f / 255 },
                { Stats.IceResistance, 30f / 255 },
                { Stats.LightningResistance, 30f / 255 },
                { Stats.FireResistance, 30f / 255 },
            });
        monster.NumberOfMaximumItemDrops = 2;
        return monster;
    }

    /// <summary>
    /// Adds the drops of the Christmas Star to the monsters of the invasion of Santa, unless they already have them.
    /// </summary>
    /// <param name="evilGoblin">The Evil Goblin.</param>
    /// <param name="cursedSanta">The Cursed Santa.</param>
    internal void AddChristmasStarDrops(MonsterDefinition evilGoblin, MonsterDefinition cursedSanta)
    {
        if (this.GameConfiguration.Items.FirstOrDefault(i => i.Group == ChristmasStarGroup && i.Number == ChristmasStarNumber) is not { } star)
        {
            return;
        }

        this.AddMonsterDrop(evilGoblin, 1, 0.05, 0, "Christmas Star (Evil Goblin)", star);
        this.AddMonsterDrop(cursedSanta, 1, 1.0, 0, "Christmas Star (Cursed Santa)", star);
        this.AddMonsterDrop(cursedSanta, 2, 0.25, 1, "Christmas Star +1 (Cursed Santa)", star);
    }

    /// <summary>
    /// Creates the gate of Santa Village where the players arrive, unless it already has a spawn gate.
    /// </summary>
    /// <param name="santaVillage">The map of Santa Village.</param>
    internal void CreateArrivalGate(GameMapDefinition santaVillage)
    {
        if (santaVillage.ExitGates.Any(g => g.IsSpawnGate))
        {
            return;
        }

        var gate = this.Context.CreateNew<ExitGate>();
        gate.Map = santaVillage;
        (gate.X1, gate.Y1, gate.X2, gate.Y2) = ArrivalArea;
        gate.Direction = Direction.South;
        gate.IsSpawnGate = true;
        santaVillage.ExitGates.Add(gate);
    }

    /// <summary>
    /// Places an NPC on a map, unless it's already there.
    /// </summary>
    /// <param name="map">The map.</param>
    /// <param name="npc">The NPC.</param>
    /// <param name="position">The position.</param>
    /// <param name="direction">The direction.</param>
    internal void CreateSpawn(GameMapDefinition map, MonsterDefinition npc, (byte X, byte Y) position, Direction direction)
    {
        if (map.MonsterSpawns.Any(s => s.MonsterDefinition?.Number == npc.Number))
        {
            return;
        }

        var spawn = this.Context.CreateNew<MonsterSpawnArea>();
        map.MonsterSpawns.Add(spawn);
        spawn.SetGuid(map.Number, npc.Number);
        spawn.GameMap = map;
        spawn.MonsterDefinition = npc;
        spawn.Quantity = 1;
        spawn.SpawnTrigger = SpawnTrigger.Automatic;
        spawn.Direction = direction;
        spawn.X1 = spawn.X2 = position.X;
        spawn.Y1 = spawn.Y2 = position.Y;
    }

    private void SetUpInvasionMonster(MonsterDefinition monster, Dictionary<AttributeDefinition, float> attributes)
    {
        monster.ObjectKind = NpcObjectKind.Monster;
        monster.NpcWindow = NpcWindow.Undefined;
        monster.AttackSkill = this.GameConfiguration.Skills.FirstOrDefault(s => s.Number == (short)SkillNumber.MonsterSkill);
        monster.MoveRange = 3;
        monster.AttackRange = 2;
        monster.ViewRange = 7;
        monster.MoveDelay = new TimeSpan(400 * TimeSpan.TicksPerMillisecond);
        monster.AttackDelay = new TimeSpan(1400 * TimeSpan.TicksPerMillisecond);

        // Invasion monsters don't respawn; only matters when they're placed as a normal spawn.
        monster.RespawnDelay = new TimeSpan(60 * TimeSpan.TicksPerSecond);
        monster.Attribute = 2;
        monster.NumberOfMaximumItemDrops = 1;
        if (monster.Attributes.Count == 0)
        {
            monster.AddAttributes(attributes, this.Context, this.GameConfiguration);
        }
    }

    private void AddMonsterDrop(MonsterDefinition monster, short number, double chance, byte itemLevel, string description, ItemDefinition item)
    {
        if (monster.DropItemGroups.Any(g => g.Description == description))
        {
            return;
        }

        var drop = this.Context.CreateNew<DropItemGroup>();
        drop.SetGuid(monster.Number, number);
        drop.Chance = chance;
        drop.ItemLevel = itemLevel;
        drop.Description = description;
        drop.Monster = monster;
        drop.PossibleItems.Add(item);
        monster.DropItemGroups.Add(drop);
        this.GameConfiguration.DropItemGroups.Add(drop);
    }

    private void AddPrize(ItemDefinition box, byte sourceLevel, double chance, string description, (byte Group, short Number, byte Level) prize)
    {
        if (this.GameConfiguration.Items.FirstOrDefault(i => i.Group == prize.Group && i.Number == prize.Number) is not { } prizeDefinition)
        {
            return;
        }

        var group = this.Context.CreateNew<ItemDropItemGroup>();
        group.Description = description;
        group.ItemType = SpecialItemType.RandomItem;
        group.SourceItemLevel = sourceLevel;
        group.Chance = chance;
        group.MinimumLevel = prize.Level;
        group.MaximumLevel = prize.Level;
        group.DropEffect = ItemDropEffect.ChristmasFireworks;
        group.PossibleItems.Add(prizeDefinition);
        box.DropItems.Add(group);
    }

    private void AddEffectOnly(ItemDefinition box, byte sourceLevel)
    {
        if (box.DropItems.Any(d => d.SourceItemLevel == sourceLevel && d.ItemType == SpecialItemType.None))
        {
            return;
        }

        var effect = this.Context.CreateNew<ItemDropItemGroup>();
        effect.Description = sourceLevel == 0 ? "Christmas Star - Drop Effect" : $"Christmas Star +{sourceLevel} - Drop Effect";
        effect.ItemType = SpecialItemType.None;
        effect.SourceItemLevel = sourceLevel;
        effect.Chance = 1.0;
        effect.DropEffect = ItemDropEffect.ChristmasFireworks;
        box.DropItems.Add(effect);
    }

    private void CreateSantaBuff(short number, string name, params (AttributeDefinition TargetAttribute, float Value, AggregateType AggregateType)[] boosts)
    {
        if (this.GameConfiguration.MagicEffects.Any(e => e.Number == number))
        {
            return;
        }

        var effect = this.Context.CreateNew<MagicEffectDefinition>();
        effect.SetGuid(number);
        this.GameConfiguration.MagicEffects.Add(effect);
        effect.Number = number;
        effect.Name = name;
        effect.InformObservers = true;
        effect.StopByDeath = true;
        effect.SubType = (byte)(number + 100);
        effect.SendDuration = true;
        effect.Duration = this.Context.CreateNew<PowerUpDefinitionValue>();
        effect.Duration.ConstantValue.Value = 30 * 60;
        foreach (var (targetAttribute, value, aggregateType) in boosts)
        {
            var powerUp = this.Context.CreateNew<PowerUpDefinition>();
            effect.PowerUpDefinitions.Add(powerUp);
            powerUp.Boost = this.Context.CreateNew<PowerUpDefinitionValue>();
            powerUp.Boost.ConstantValue.Value = value;
            powerUp.Boost.ConstantValue.AggregateType = aggregateType;
            powerUp.TargetAttribute = targetAttribute.GetPersistent(this.GameConfiguration);
        }
    }
}
