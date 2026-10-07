// <copyright file="BossEncounterTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.PlugIns.BossEncounters;
using MUnique.OpenMU.Pathfinding;
using MonsterAttribute = MUnique.OpenMU.Persistence.BasicModel.MonsterAttribute;
using MonsterDefinition = MUnique.OpenMU.Persistence.BasicModel.MonsterDefinition;

/// <summary>
/// Tests for the boss encounters of the <see cref="BossEncounterPlugIn"/>.
/// </summary>
[TestFixture]
public class BossEncounterTest
{
    private const short BossNumber = 900;
    private const short PillarNumber = 901;
    private const short CrystalNumber = 902;
    private const short AddNumber = 903;
    private const int BossHealth = 1000;

    private static readonly DateTime Start = new(2026, 10, 7, 21, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Tests that the phase changes when the health of the boss falls to its threshold, and that it doesn't go back when the boss heals.
    /// </summary>
    [Test]
    public void PhaseFollowsHealthAndDoesNotGoBack()
    {
        var thresholds = new[] { 100, 75, 50, 20 };

        Assert.That(BossEncounterPhase.GetPhaseIndex(thresholds, 100, -1), Is.EqualTo(0));
        Assert.That(BossEncounterPhase.GetPhaseIndex(thresholds, 75, 0), Is.EqualTo(1));
        Assert.That(BossEncounterPhase.GetPhaseIndex(thresholds, 76, 0), Is.EqualTo(0));
        Assert.That(BossEncounterPhase.GetPhaseIndex(thresholds, 10, 0), Is.EqualTo(3));
        Assert.That(BossEncounterPhase.GetPhaseIndex(thresholds, 90, 2), Is.EqualTo(2));
    }

    /// <summary>
    /// Tests that the next start of a schedule entry is found on the same day, the next day, or the configured day of the week.
    /// </summary>
    [Test]
    public void ScheduleFindsNextStart()
    {
        var everyDay = new BossEncounterScheduleEntry { Day = BossEncounterDay.EveryDay, Time = new TimeOnly(21, 0) };
        var monday = new BossEncounterScheduleEntry { Day = BossEncounterDay.Monday, Time = new TimeOnly(15, 0) };

        // 2026-10-07 is a wednesday.
        Assert.That(everyDay.GetNextStart(new DateTime(2026, 10, 7, 20, 0, 0)), Is.EqualTo(new DateTime(2026, 10, 7, 21, 0, 0)));
        Assert.That(everyDay.GetNextStart(new DateTime(2026, 10, 7, 21, 0, 1)), Is.EqualTo(new DateTime(2026, 10, 8, 21, 0, 0)));
        Assert.That(monday.GetNextStart(new DateTime(2026, 10, 7, 20, 0, 0)), Is.EqualTo(new DateTime(2026, 10, 12, 15, 0, 0)));
    }

    /// <summary>
    /// Tests that the encounter starts at its scheduled time, with the multiplied health of the boss at the configured point.
    /// </summary>
    [Test]
    public async Task EncounterStartsAtScheduledTimeAsync()
    {
        var gameContext = CreateGameContext();
        var definition = CreateDefinition();
        definition.Schedule.Add(new BossEncounterScheduleEntry { Day = BossEncounterDay.EveryDay, Time = TimeOnly.FromDateTime(Start) });
        var context = new BossEncounterContext(gameContext, definition);

        await context.TickAsync(Start.AddMinutes(-1)).ConfigureAwait(false);
        Assert.That(context.IsRunning, Is.False);

        await context.TickAsync(Start).ConfigureAwait(false);
        Assert.That(context.IsRunning, Is.True);
        Assert.That(context.Boss!.Position, Is.EqualTo(new Point(100, 100)));
        Assert.That(context.Boss.Attributes[Stats.MaximumHealth], Is.EqualTo(BossHealth * 2));
        Assert.That(context.Boss.Health, Is.EqualTo(BossHealth * 2));
    }

    /// <summary>
    /// Tests that each living pillar reduces the damage which the boss receives.
    /// </summary>
    [Test]
    public async Task PillarsReduceDamageOfBossAsync()
    {
        var (context, _) = await StartEncounterAsync().ConfigureAwait(false);

        Assert.That(context.CurrentPhase!.Name, Is.EqualTo("Pilares"));
        Assert.That(context.Objects, Has.Count.EqualTo(2));
        Assert.That(context.Boss!.Attributes[Stats.DamageReceiveDecrement], Is.EqualTo(0.6f).Within(0.001f));

        await DestroyAsync(context.Objects[0]).ConfigureAwait(false);
        await context.TickAsync(Start.AddSeconds(1)).ConfigureAwait(false);
        Assert.That(context.Boss.Attributes[Stats.DamageReceiveDecrement], Is.EqualTo(0.8f).Within(0.001f));

        await DestroyAsync(context.Objects[1]).ConfigureAwait(false);
        await context.TickAsync(Start.AddSeconds(2)).ConfigureAwait(false);
        Assert.That(context.Boss.Attributes[Stats.DamageReceiveDecrement], Is.EqualTo(1f).Within(0.001f));
    }

    /// <summary>
    /// Tests that the boss is invulnerable in the shield phase until the crystals are destroyed.
    /// </summary>
    [Test]
    public async Task ShieldFallsWhenCrystalsAreDestroyedAsync()
    {
        var (context, _) = await StartEncounterAsync().ConfigureAwait(false);
        context.Boss!.Health = (int)(context.Boss.Attributes[Stats.MaximumHealth] * 0.7);

        await context.TickAsync(Start.AddSeconds(1)).ConfigureAwait(false);
        Assert.That(context.CurrentPhase!.Name, Is.EqualTo("Escudo"));
        Assert.That(context.Objects.Select(o => o.Definition.Number), Is.All.EqualTo(CrystalNumber));
        Assert.That(context.Boss.Attributes[Stats.DamageReceiveDecrement], Is.EqualTo(0));

        foreach (var crystal in context.Objects.ToList())
        {
            await DestroyAsync(crystal).ConfigureAwait(false);
        }

        await context.TickAsync(Start.AddSeconds(2)).ConfigureAwait(false);
        Assert.That(context.Boss.Attributes[Stats.DamageReceiveDecrement], Is.EqualTo(1f).Within(0.001f));
    }

    /// <summary>
    /// Tests that the crystals appear again and the boss heals, when the crystals aren't destroyed within the window.
    /// </summary>
    [Test]
    public async Task ShieldFailureHealsBossAndRespawnsCrystalsAsync()
    {
        var (context, player) = await StartEncounterAsync().ConfigureAwait(false);
        var boss = context.Boss!;
        var maximumHealth = (int)boss.Attributes[Stats.MaximumHealth];
        boss.Health = (int)(maximumHealth * 0.7);
        await context.TickAsync(Start.AddSeconds(1)).ConfigureAwait(false);

        var firstCrystal = context.Objects[0];
        await DestroyAsync(firstCrystal).ConfigureAwait(false);
        await context.TickAsync(Start.AddSeconds(2)).ConfigureAwait(false);
        await context.TickAsync(Start.AddSeconds(13)).ConfigureAwait(false);

        Assert.That(context.Objects, Has.Count.EqualTo(2));
        Assert.That(context.Objects, Has.All.Matches<Monster>(o => o.IsAlive && o != firstCrystal));
        Assert.That(boss.Health, Is.EqualTo((int)(maximumHealth * 0.7) + (maximumHealth / 10)));
        Assert.That(player.Attributes![Stats.CurrentHealth], Is.LessThan(player.Attributes[Stats.MaximumHealth]));
        Assert.That(boss.Attributes[Stats.DamageReceiveDecrement], Is.EqualTo(0));
    }

    /// <summary>
    /// Tests that the area attack hits the marked position after the delay, but not before.
    /// </summary>
    [Test]
    public async Task AreaAttackHitsMarkedPlayerAfterDelayAsync()
    {
        var (context, player) = await StartEncounterAsync().ConfigureAwait(false);
        context.Boss!.Health = (int)(context.Boss.Attributes[Stats.MaximumHealth] * 0.4);
        await context.TickAsync(Start.AddSeconds(1)).ConfigureAwait(false);
        Assert.That(context.CurrentPhase!.Name, Is.EqualTo("Meteoritos"));
        Assert.That(context.Summons, Has.Count.EqualTo(2));

        var maximumHealth = player.Attributes![Stats.MaximumHealth];
        await context.TickAsync(Start.AddSeconds(9)).ConfigureAwait(false);
        Assert.That(context.PendingAreaAttackCount, Is.EqualTo(1));
        Assert.That(player.Attributes[Stats.CurrentHealth], Is.EqualTo(maximumHealth));

        await context.TickAsync(Start.AddSeconds(12)).ConfigureAwait(false);
        Assert.That(context.PendingAreaAttackCount, Is.EqualTo(0));
        Assert.That(player.Attributes[Stats.CurrentHealth], Is.EqualTo(maximumHealth - (uint)(maximumHealth * 0.4)).Within(1));
    }

    /// <summary>
    /// Tests that the boss retreats when the maximum duration is reached, and that its monsters are removed.
    /// </summary>
    [Test]
    public async Task BossRetreatsAfterMaximumDurationAsync()
    {
        var (context, _) = await StartEncounterAsync().ConfigureAwait(false);
        var boss = context.Boss!;
        var pillars = context.Objects.ToList();

        await context.TickAsync(Start.AddMinutes(20)).ConfigureAwait(false);

        Assert.That(context.IsRunning, Is.False);
        Assert.That(boss.IsAlive, Is.False);
        Assert.That(pillars, Has.All.Matches<Monster>(o => !o.IsAlive));
    }

    /// <summary>
    /// Tests that the monster of the boss, which appears on the map by itself, is hidden during the encounter and shown again afterward.
    /// </summary>
    [Test]
    public async Task AutomaticSpawnIsHiddenDuringEncounterAsync()
    {
        var gameContext = CreateGameContext();
        var map = (await gameContext.GetMapAsync(0).ConfigureAwait(false))!;
        var automaticBoss = await SpawnAsync(gameContext, map, BossNumber, new Point(50, 50), SpawnTrigger.Automatic).ConfigureAwait(false);
        var context = new BossEncounterContext(gameContext, CreateDefinition());

        await context.StartAsync(Start).ConfigureAwait(false);
        Assert.That(map.GetNpcsInRange(new Point(50, 50), 1), Does.Not.Contain(automaticBoss));

        context.RequestStop();
        await context.TickAsync(Start.AddSeconds(1)).ConfigureAwait(false);
        Assert.That(context.IsRunning, Is.False);
        Assert.That(map.GetNpcsInRange(new Point(50, 50), 1), Does.Contain(automaticBoss));
    }

    private static async ValueTask<(BossEncounterContext Context, Player Player)> StartEncounterAsync()
    {
        var gameContext = CreateGameContext();
        var context = new BossEncounterContext(gameContext, CreateDefinition());
        var player = await CreatePlayerAsync(gameContext, new Point(105, 100)).ConfigureAwait(false);
        Assert.That(await context.StartAsync(Start).ConfigureAwait(false), Is.True);
        return (context, player);
    }

    private static IGameContext CreateGameContext()
    {
        var gameContext = GameContextTestHelper.CreateGameContext();
        gameContext.Configuration.Monsters.Add(CreateMonsterDefinition(BossNumber, BossHealth));
        gameContext.Configuration.Monsters.Add(CreateMonsterDefinition(PillarNumber, 100));
        gameContext.Configuration.Monsters.Add(CreateMonsterDefinition(CrystalNumber, 100));
        gameContext.Configuration.Monsters.Add(CreateMonsterDefinition(AddNumber, 100));
        return gameContext;
    }

    private static MonsterDefinition CreateMonsterDefinition(short number, float health)
    {
        var definition = new MonsterDefinition { Id = Guid.NewGuid(), Number = number, ObjectKind = NpcObjectKind.Monster, AttackDelay = TimeSpan.FromHours(1) };
        definition.Attributes.Add(new MonsterAttribute { AttributeDefinition = Stats.MaximumHealth, Value = health });
        return definition;
    }

    private static BossEncounterDefinition CreateDefinition() => new()
    {
        Name = "Test",
        MonsterNumber = BossNumber,
        HealthMultiplier = 2,
        MapNumber = 0,
        SpawnPoints = new List<BossEncounterPoint> { new() { X = 100, Y = 100 } },
        ArenaRadius = 18,
        MaximumDuration = TimeSpan.FromMinutes(20),
        Phases = new List<BossEncounterPhase>
        {
            new()
            {
                Name = "Pilares",
                HealthThreshold = 100,
                Objects = new List<BossEncounterObjectSpawn>
                {
                    new() { MonsterNumber = PillarNumber, X = 95, Y = 95 },
                    new() { MonsterNumber = PillarNumber, X = 110, Y = 110 },
                },
                DamageReductionPerObject = 20,
            },
            new()
            {
                Name = "Escudo",
                HealthThreshold = 75,
                Objects = new List<BossEncounterObjectSpawn>
                {
                    new() { MonsterNumber = CrystalNumber, X = 90, Y = 100 },
                    new() { MonsterNumber = CrystalNumber, X = 112, Y = 100 },
                },
                IsBossInvulnerableWhileObjectsAlive = true,
                ObjectsTimeLimit = TimeSpan.FromSeconds(60),
                ObjectsKillWindow = TimeSpan.FromSeconds(10),
                FailureDamagePercent = 50,
                FailureBossHealPercent = 10,
            },
            new()
            {
                Name = "Meteoritos",
                HealthThreshold = 50,
                Summons = new List<BossEncounterSummon> { new() { MonsterNumber = AddNumber, Quantity = 2 } },
                AreaAttackInterval = TimeSpan.FromSeconds(8),
                AreaAttackTargets = 1,
                AreaAttackRadius = 3,
                AreaAttackDelay = TimeSpan.FromSeconds(3),
                AreaAttackDamagePercent = 40,
            },
        },
    };

    private static async ValueTask<Monster> SpawnAsync(IGameContext gameContext, GameMap map, short number, Point point, SpawnTrigger trigger)
    {
        var definition = gameContext.Configuration.Monsters.First(m => m.Number == number);
        var spawnArea = new MonsterSpawnArea
        {
            MonsterDefinition = definition,
            GameMap = map.Definition,
            X1 = point.X,
            Y1 = point.Y,
            X2 = point.X,
            Y2 = point.Y,
            Quantity = 1,
            SpawnTrigger = trigger,
        };

        var monster = new Monster(spawnArea, definition, map, NullDropGenerator.Instance, new NullMonsterIntelligence(), gameContext.PlugInManager, gameContext.PathFinderPool);
        monster.Initialize();
        await map.AddAsync(monster).ConfigureAwait(false);
        return monster;
    }

    private static async ValueTask DestroyAsync(Monster monster)
    {
        await monster.CurrentMap.RemoveAsync(monster).ConfigureAwait(false);
        monster.Dispose();
    }

    private static async ValueTask<Player> CreatePlayerAsync(IGameContext gameContext, Point position)
    {
        var player = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        await player.PlayerState.TryAdvanceToAsync(PlayerState.EnteredWorld).ConfigureAwait(false);
        player.IsAlive = true;
        player.Position = position;
        player.Attributes![Stats.CurrentHealth] = player.Attributes[Stats.MaximumHealth];
        await player.CurrentMap!.AddAsync(player).ConfigureAwait(false);
        return player;
    }
}
