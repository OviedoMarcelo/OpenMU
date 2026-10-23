// <copyright file="BossEncounterTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.PlugIns.BossEncounters;
using MUnique.OpenMU.GameLogic.Views;
using MUnique.OpenMU.GameLogic.Views.World;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Pathfinding;
using Moq;
using ItemDefinition = MUnique.OpenMU.Persistence.BasicModel.ItemDefinition;
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
    /// Tests that a schedule entry with a repetition starts again in its interval until the end of the day.
    /// </summary>
    [Test]
    public void ScheduleRepeatsUntilEndOfDay()
    {
        var entry = new BossEncounterScheduleEntry { Day = BossEncounterDay.EveryDay, Time = new TimeOnly(12, 0), RepeatEvery = TimeSpan.FromHours(4) };

        Assert.That(entry.GetNextStart(new DateTime(2026, 10, 7, 11, 0, 0)), Is.EqualTo(new DateTime(2026, 10, 7, 12, 0, 0)));
        Assert.That(entry.GetNextStart(new DateTime(2026, 10, 7, 13, 0, 0)), Is.EqualTo(new DateTime(2026, 10, 7, 16, 0, 0)));
        Assert.That(entry.GetNextStart(new DateTime(2026, 10, 7, 20, 0, 0)), Is.EqualTo(new DateTime(2026, 10, 7, 20, 0, 0)));
        Assert.That(entry.GetNextStart(new DateTime(2026, 10, 7, 20, 0, 1)), Is.EqualTo(new DateTime(2026, 10, 8, 12, 0, 0)));
    }

    /// <summary>
    /// Tests that the random delay keeps the start within its window, and that the encounter starts at the delayed time.
    /// </summary>
    [Test]
    public async Task RandomDelayKeepsStartWithinWindowAsync()
    {
        var definition = CreateDefinition();
        definition.Schedule.Add(new BossEncounterScheduleEntry { Day = BossEncounterDay.EveryDay, Time = TimeOnly.FromDateTime(Start), RandomDelayUpTo = TimeSpan.FromMinutes(30) });
        for (var i = 0; i < 50; i++)
        {
            Assert.That(definition.GetNextStart(Start.AddHours(-1)), Is.InRange(Start, Start.AddMinutes(30)));
        }

        var context = new BossEncounterContext(CreateGameContext(), definition);
        await context.TickAsync(Start.AddHours(-1)).ConfigureAwait(false);
        var nextStart = DateTime.SpecifyKind(context.NextStart!.Value, DateTimeKind.Utc);

        await context.TickAsync(nextStart.AddSeconds(-1)).ConfigureAwait(false);
        Assert.That(context.IsRunning, Is.False);
        await context.TickAsync(nextStart).ConfigureAwait(false);
        Assert.That(context.IsRunning, Is.True);
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
    /// Tests that an object which blocks the way blocks its tile while it's alive, and that the others don't.
    /// </summary>
    [Test]
    public async Task BlockingObjectBlocksItsTileUntilDestroyedAsync()
    {
        var definition = CreateDefinition();
        definition.Phases.First().Objects.First().IsBlocking = true;
        var (context, _) = await StartEncounterAsync(definition).ConfigureAwait(false);
        var blocking = context.Objects[0];
        var other = context.Objects[1];
        var terrain = blocking.CurrentMap.Terrain;
        bool IsBlocked(Point tile) => (terrain.AttributeMap[tile.X, tile.Y] & (byte)TerrainAttributeType.Blocked) != 0;

        Assert.That(IsBlocked(blocking.Position), Is.True);
        Assert.That(IsBlocked(other.Position), Is.False);

        await DestroyAsync(blocking).ConfigureAwait(false);
        await context.TickAsync(Start.AddSeconds(1)).ConfigureAwait(false);
        Assert.That(IsBlocked(blocking.Position), Is.False);
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

        var groundMarker = Mock.Get(player.ViewPlugIns.GetPlugIn<IShowGroundMarkerPlugIn>()!);
        groundMarker.Verify(p => p.ShowGroundWarningAsync(new Point(105, 100), 3, TimeSpan.FromSeconds(3)), Times.Once);
        groundMarker.Verify(p => p.ShowGroundHitAsync(new Point(105, 100), 3), Times.Once);
    }

    /// <summary>
    /// Tests that the bar of the health of the boss is shown to the players in the arena when it changes, and hidden when the encounter ends.
    /// </summary>
    [Test]
    public async Task HealthBarIsShownInArenaAndHiddenAtEndAsync()
    {
        var (context, player) = await StartEncounterAsync().ConfigureAwait(false);
        var boss = context.Boss!;
        var healthBar = Mock.Get(player.ViewPlugIns.GetPlugIn<IShowBossHealthPlugIn>()!);

        await context.TickAsync(Start.AddSeconds(1)).ConfigureAwait(false);
        await context.TickAsync(Start.AddSeconds(2)).ConfigureAwait(false);
        healthBar.Verify(p => p.ShowBossHealthAsync(boss, "Test", "Pilares", 100), Times.Once);

        boss.Health = (int)(boss.Attributes[Stats.MaximumHealth] * 0.4);
        await context.TickAsync(Start.AddSeconds(3)).ConfigureAwait(false);
        healthBar.Verify(p => p.ShowBossHealthAsync(boss, "Test", "Meteoritos", 40), Times.Once);

        context.RequestStop();
        await context.TickAsync(Start.AddSeconds(4)).ConfigureAwait(false);
        healthBar.Verify(p => p.HideBossHealthAsync(boss), Times.Once);
    }

    /// <summary>
    /// Tests that each configured percentage of the health is announced once, and only the lowest one when several are reached at once.
    /// </summary>
    [Test]
    public async Task HealthIsAnnouncedOnceAsync()
    {
        var gameContext = CreateGameContext();
        var definition = CreateDefinition();
        definition.HealthAnnouncements = new List<int> { 90, 75, 50 };
        definition.HealthAnnouncementMessage = "Vida {1}";
        var context = new BossEncounterContext(gameContext, definition);
        var player = await CreatePlayerAsync(gameContext, new Point(105, 100)).ConfigureAwait(false);
        await context.StartAsync(Start).ConfigureAwait(false);
        var boss = context.Boss!;
        var messages = Mock.Get(player.ViewPlugIns.GetPlugIn<IShowMessagePlugIn>()!);

        boss.Health = (int)(boss.Attributes[Stats.MaximumHealth] * 0.7);
        await context.TickAsync(Start.AddSeconds(1)).ConfigureAwait(false);
        await context.TickAsync(Start.AddSeconds(2)).ConfigureAwait(false);
        boss.Health = (int)(boss.Attributes[Stats.MaximumHealth] * 0.4);
        await context.TickAsync(Start.AddSeconds(3)).ConfigureAwait(false);

        messages.Verify(p => p.ShowMessageAsync("Vida 90", It.IsAny<MessageType>()), Times.Never);
        messages.Verify(p => p.ShowMessageAsync("Vida 75", MessageType.GoldenCenter), Times.Once);
        messages.Verify(p => p.ShowMessageAsync("Vida 50", MessageType.GoldenCenter), Times.Once);
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
    /// Tests that the boss is teleported back to the place where it appeared, when it's pulled too far away.
    /// </summary>
    [Test]
    public async Task BossIsTeleportedBackWhenPulledAwayAsync()
    {
        var (context, _) = await StartEncounterAsync().ConfigureAwait(false);
        var boss = context.Boss!;

        await boss.MoveAsync(new Point(120, 100)).ConfigureAwait(false);
        await context.TickAsync(Start.AddSeconds(1)).ConfigureAwait(false);
        Assert.That(boss.Position, Is.EqualTo(new Point(120, 100)));

        await boss.MoveAsync(new Point(140, 100)).ConfigureAwait(false);
        await context.TickAsync(Start.AddSeconds(2)).ConfigureAwait(false);
        Assert.That(boss.Position, Is.EqualTo(new Point(100, 100)));
    }

    /// <summary>
    /// Tests that the boss heals completely and goes back to its first phase, when no living player was in the arena for the configured time.
    /// </summary>
    [Test]
    public async Task EncounterStartsAgainWhenArenaIsEmptyAsync()
    {
        var (context, player) = await StartEncounterAsync().ConfigureAwait(false);
        var boss = context.Boss!;
        var maximumHealth = (int)boss.Attributes[Stats.MaximumHealth];
        boss.Health = (int)(maximumHealth * 0.4);
        await context.TickAsync(Start.AddSeconds(1)).ConfigureAwait(false);
        Assert.That(context.CurrentPhase!.Name, Is.EqualTo("Meteoritos"));

        player.IsAlive = false;
        await context.TickAsync(Start.AddSeconds(2)).ConfigureAwait(false);
        await context.TickAsync(Start.AddSeconds(31)).ConfigureAwait(false);
        Assert.That(boss.Health, Is.EqualTo((int)(maximumHealth * 0.4)));

        await context.TickAsync(Start.AddSeconds(32)).ConfigureAwait(false);
        Assert.That(context.IsRunning, Is.True);
        Assert.That(boss.Health, Is.EqualTo(maximumHealth));
        Assert.That(context.CurrentPhase!.Name, Is.EqualTo("Pilares"));
        Assert.That(context.Summons, Is.Empty);
        Assert.That(context.Objects, Has.Count.EqualTo(2));
    }

    /// <summary>
    /// Tests that the encounter doesn't start again while a living player is in the arena.
    /// </summary>
    [Test]
    public async Task EncounterDoesNotStartAgainWithPlayersInArenaAsync()
    {
        var (context, _) = await StartEncounterAsync().ConfigureAwait(false);
        var boss = context.Boss!;
        var health = (int)(boss.Attributes[Stats.MaximumHealth] * 0.4);
        boss.Health = health;

        await context.TickAsync(Start.AddSeconds(1)).ConfigureAwait(false);
        await context.TickAsync(Start.AddSeconds(40)).ConfigureAwait(false);

        Assert.That(boss.Health, Is.EqualTo(health));
        Assert.That(context.CurrentPhase!.Name, Is.EqualTo("Meteoritos"));
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

    /// <summary>
    /// Tests that the boss gets the configured appearance before it appears, so that it's sent with the monster.
    /// </summary>
    [Test]
    public async Task BossGetsConfiguredAppearanceAsync()
    {
        var gameContext = CreateGameContext();
        var definition = CreateDefinition();
        definition.ScalePercent = 150;
        definition.Glow = MonsterGlow.Level15;
        definition.DisplayName = "Elite Boss";
        var context = new BossEncounterContext(gameContext, definition);

        await context.StartAsync(Start).ConfigureAwait(false);

        Assert.That(MonsterAppearances.Get(context.Boss!), Is.EqualTo(new MonsterAppearance(150, MonsterGlow.Level15, "Elite Boss")));
        Assert.That(MonsterAppearances.Get(context.Objects[0]), Is.Null);
    }

    /// <summary>
    /// Tests that a configuration without size, which was stored before the size existed, shows the boss in its normal size.
    /// </summary>
    [Test]
    public void MissingSizeMeansNormalSize()
    {
        var definition = new BossEncounterDefinition { ScalePercent = 0 };

        Assert.That(definition.GetBossAppearance().IsDefault, Is.True);
    }

    /// <summary>
    /// Tests that the configured drops are generated with their level and quantity, and that a chance of 0 drops nothing.
    /// </summary>
    [Test]
    public async Task BossDropsConfiguredItemsAsync()
    {
        var gameContext = CreateGameContext();
        var itemDefinition = AddItemDefinition(gameContext);
        var definition = CreateDefinition();
        definition.Drops.Add(new BossEncounterDrop { Item = itemDefinition, Chance = 100, Quantity = 2, ItemLevel = 9 });
        definition.Drops.Add(new BossEncounterDrop { Item = itemDefinition, Chance = 0, Quantity = 5 });
        definition.ReplaceMonsterDrops = true;
        definition.DropMoney = 1000;
        var generator = new BossEncounterDropGenerator(CreateMonsterDropGenerator(itemDefinition), definition, gameContext);
        var player = await CreatePlayerAsync(gameContext, new Point(100, 100)).ConfigureAwait(false);

        var (items, money) = await generator.GenerateItemDropsAsync(gameContext.Configuration.Monsters.First(), 0, player).ConfigureAwait(false);

        Assert.That(items.ToList(), Has.Count.EqualTo(2));
        Assert.That(items, Has.All.Matches<Item>(item => item.Definition == itemDefinition && item.Level == 9));
        Assert.That(money, Is.EqualTo(1000));
    }

    /// <summary>
    /// Tests that the drops of the monster stay, unless they're replaced.
    /// </summary>
    [Test]
    public async Task BossKeepsMonsterDropsUnlessReplacedAsync()
    {
        var gameContext = CreateGameContext();
        var itemDefinition = AddItemDefinition(gameContext);
        var definition = CreateDefinition();
        definition.Drops.Add(new BossEncounterDrop { Item = itemDefinition, Chance = 100, Quantity = 1, ItemLevel = 13 });
        definition.DropMoney = 1000;
        var generator = new BossEncounterDropGenerator(CreateMonsterDropGenerator(itemDefinition), definition, gameContext);
        var player = await CreatePlayerAsync(gameContext, new Point(100, 100)).ConfigureAwait(false);

        var (items, money) = await generator.GenerateItemDropsAsync(gameContext.Configuration.Monsters.First(), 0, player).ConfigureAwait(false);

        Assert.That(items.Select(i => (int)i.Level), Is.EquivalentTo(new[] { 0, 13 }));
        Assert.That(money, Is.EqualTo(1500));
    }

    /// <summary>
    /// Tests that the damage of the hits on the boss is summed up by player, that the damage of a summoned monster
    /// counts for its owner, and that hits on other monsters aren't tracked.
    /// </summary>
    [Test]
    public async Task DamageIsTrackedByPlayerAsync()
    {
        var (context, player) = await StartEncounterAsync().ConfigureAwait(false);
        var other = await CreatePlayerAsync(player.GameContext, new Point(106, 100)).ConfigureAwait(false);
        var summon = await SpawnAsync(player.GameContext, context.Boss!.CurrentMap, AddNumber, new Point(104, 100), SpawnTrigger.Automatic, new SummonedMonsterIntelligence(other)).ConfigureAwait(false);
        var plugIn = new BossEncounterHitPlugIn();

        plugIn.AttackableGotHit(context.Boss, player, new HitInfo(250, 50, DamageAttributes.Undefined));
        plugIn.AttackableGotHit(context.Boss, other, new HitInfo(60, 0, DamageAttributes.Undefined));
        plugIn.AttackableGotHit(context.Boss, summon, new HitInfo(40, 0, DamageAttributes.Undefined));
        plugIn.AttackableGotHit(context.Objects[0], player, new HitInfo(1000, 0, DamageAttributes.Undefined));

        var participants = context.DamageTracker.GetParticipants();
        Assert.That(participants.Select(p => (p.Player, p.Damage)), Is.EqualTo(new[] { (player, 300L), (other, 100L) }));
        Assert.That(participants[0].Percent, Is.EqualTo(75).Within(0.001));
    }

    /// <summary>
    /// Tests that the players with enough damage get the rewards for the participation when the boss is defeated, and the others don't.
    /// </summary>
    [Test]
    public async Task ParticipantsWithEnoughDamageGetRewardsAsync()
    {
        var gameContext = CreateGameContext();
        var itemDefinition = AddItemDefinition(gameContext);
        var definition = CreateDefinition();
        definition.ParticipationMinimumDamagePercent = 20;
        definition.ParticipationRewards.Add(new BossEncounterDrop { Item = itemDefinition, Chance = 100, Quantity = 1, ItemLevel = 7 });
        definition.ParticipationMoney = 1000;
        var context = new BossEncounterContext(gameContext, definition);
        var player = await CreatePlayerAsync(gameContext, new Point(105, 100)).ConfigureAwait(false);
        var other = await CreatePlayerAsync(gameContext, new Point(106, 100)).ConfigureAwait(false);
        await context.StartAsync(Start).ConfigureAwait(false);
        var boss = context.Boss!;

        context.DamageTracker.AddDamage(player, 900);
        context.DamageTracker.AddDamage(other, 100);
        await boss.ReflectDamageAsync(player, (uint)boss.Health).ConfigureAwait(false);
        await context.TickAsync(Start.AddSeconds(1)).ConfigureAwait(false);

        Assert.That(context.IsRunning, Is.False);
        Assert.That(player.Inventory!.Items.Where(i => i.Definition == itemDefinition).Select(i => (int)i.Level), Is.EqualTo(new[] { 7 }));
        Assert.That(player.Money, Is.EqualTo(1000));
        Assert.That(other.Inventory!.Items, Is.Empty);
        Assert.That(other.Money, Is.EqualTo(0));
    }

    /// <summary>
    /// Tests that the healing of players in the arena is tracked for the healer, and the healing of players outside of it isn't.
    /// </summary>
    [Test]
    public async Task HealingInArenaIsTrackedAsync()
    {
        var definition = CreateDefinition();
        definition.IsHealingTracked = true;
        var (context, player) = await StartEncounterAsync(definition).ConfigureAwait(false);
        var healer = await CreatePlayerAsync(player.GameContext, new Point(106, 100)).ConfigureAwait(false);
        var outside = await CreatePlayerAsync(player.GameContext, new Point(160, 160)).ConfigureAwait(false);
        var plugIn = new BossEncounterHealPlugIn();

        plugIn.HealthRestored(player, healer, 300);
        plugIn.HealthRestored(healer, healer, 50);
        plugIn.HealthRestored(outside, healer, 1000);
        plugIn.HealthRestored(context.Boss!, healer, 1000);

        var participant = context.DamageTracker.GetParticipants().Single();
        Assert.That(participant.Player, Is.SameAs(healer));
        Assert.That(participant.Healing, Is.EqualTo(350));
        Assert.That(participant.Damage, Is.EqualTo(0));
    }

    /// <summary>
    /// Tests that the healing isn't tracked when the encounter doesn't take it into account, which is the default.
    /// </summary>
    [Test]
    public async Task HealingIsNotTrackedByDefaultAsync()
    {
        var (context, player) = await StartEncounterAsync().ConfigureAwait(false);
        var healer = await CreatePlayerAsync(player.GameContext, new Point(106, 100)).ConfigureAwait(false);

        new BossEncounterHealPlugIn().HealthRestored(player, healer, 300);

        Assert.That(context.DamageTracker.GetParticipants(), Is.Empty);
    }

    /// <summary>
    /// Tests that the best places of the rankings of damage and healing get their rewards, in addition to each other.
    /// </summary>
    [Test]
    public async Task RankRewardsGoToBestPlacesAsync()
    {
        var gameContext = CreateGameContext();
        var itemDefinition = AddItemDefinition(gameContext);
        var definition = CreateDefinition();
        definition.RankRewards.Add(new BossEncounterRankReward { Ranking = BossEncounterRanking.Damage, FromRank = 1, ToRank = 1, Item = itemDefinition, ItemLevel = 13 });
        definition.RankRewards.Add(new BossEncounterRankReward { Ranking = BossEncounterRanking.Damage, FromRank = 2, ToRank = 3, Item = itemDefinition, ItemLevel = 9 });
        definition.RankRewards.Add(new BossEncounterRankReward { Ranking = BossEncounterRanking.Healing, FromRank = 1, ToRank = 1, Money = 5000 });
        var context = new BossEncounterContext(gameContext, definition);
        var first = await CreatePlayerAsync(gameContext, new Point(105, 100)).ConfigureAwait(false);
        var second = await CreatePlayerAsync(gameContext, new Point(106, 100)).ConfigureAwait(false);
        var healer = await CreatePlayerAsync(gameContext, new Point(107, 100)).ConfigureAwait(false);
        await context.StartAsync(Start).ConfigureAwait(false);
        var boss = context.Boss!;

        context.DamageTracker.AddDamage(second, 100);
        context.DamageTracker.AddHealing(healer, 500);
        await boss.ReflectDamageAsync(first, (uint)boss.Health).ConfigureAwait(false);
        await context.TickAsync(Start.AddSeconds(1)).ConfigureAwait(false);

        Assert.That(first.Inventory!.Items.Select(i => (int)i.Level), Is.EqualTo(new[] { 13 }));
        Assert.That(second.Inventory!.Items.Select(i => (int)i.Level), Is.EqualTo(new[] { 9 }));
        Assert.That(healer.Inventory!.Items, Is.Empty);
        Assert.That(healer.Money, Is.EqualTo(5000));
        Assert.That(first.Money, Is.EqualTo(0));
    }

    /// <summary>
    /// Tests that a reward which doesn't fit into the inventory is dropped at the feet of the player, for the player.
    /// </summary>
    [Test]
    public async Task RewardIsDroppedWhenInventoryIsFullAsync()
    {
        var gameContext = CreateGameContext();
        var itemDefinition = AddItemDefinition(gameContext);
        itemDefinition.Width = 8;
        itemDefinition.Height = 15;
        var player = await CreatePlayerAsync(gameContext, new Point(105, 100)).ConfigureAwait(false);
        var filler = new TemporaryItem { Definition = itemDefinition }.MakePersistent(player.PersistenceContext);
        Assume.That(await player.Inventory!.AddItemAsync(filler).ConfigureAwait(false), Is.True);

        var reward = new TemporaryItem { Definition = itemDefinition, Level = 9 };
        await BossEncounterRewarder.GiveAsync(player, [reward], 0).ConfigureAwait(false);

        Assert.That(player.Inventory.Items, Has.Count.EqualTo(1));
        var drop = player.CurrentMap!.GetDropsInRange(player.Position, 0).OfType<DroppedItem>().Single();
        Assert.That(drop.Item, Is.SameAs(reward));
    }

    /// <summary>
    /// Tests that the boss gets more health and damage with more players in the arena, keeping the percentage of its health,
    /// and that it doesn't get weaker when they leave.
    /// </summary>
    [Test]
    public async Task BossScalesWithPlayersInArenaAsync()
    {
        var gameContext = CreateGameContext();
        var definition = CreateDefinition();
        definition.ScalingHealthPercentPerPlayer = 50;
        definition.ScalingDamagePercentPerPlayer = 10;
        var context = new BossEncounterContext(gameContext, definition);
        await CreatePlayerAsync(gameContext, new Point(105, 100)).ConfigureAwait(false);
        await context.StartAsync(Start).ConfigureAwait(false);
        var boss = context.Boss!;
        var damage = boss.Attributes[Stats.AttackDamageIncrease];
        Assume.That(damage, Is.GreaterThan(0));
        boss.Health = BossHealth;

        await context.TickAsync(Start.AddSeconds(1)).ConfigureAwait(false);
        Assert.That(boss.Attributes[Stats.MaximumHealth], Is.EqualTo(BossHealth * 2));

        var others = new[]
        {
            await CreatePlayerAsync(gameContext, new Point(106, 100)).ConfigureAwait(false),
            await CreatePlayerAsync(gameContext, new Point(107, 100)).ConfigureAwait(false),
        };
        await context.TickAsync(Start.AddSeconds(11)).ConfigureAwait(false);
        Assert.That(boss.Attributes[Stats.MaximumHealth], Is.EqualTo(BossHealth * 4));
        Assert.That(boss.Health, Is.EqualTo(BossHealth * 2));
        Assert.That(boss.Attributes[Stats.AttackDamageIncrease], Is.EqualTo(damage * 1.2f).Within(0.001f));

        foreach (var other in others)
        {
            other.IsAlive = false;
        }

        await context.TickAsync(Start.AddSeconds(21)).ConfigureAwait(false);
        Assert.That(boss.Attributes[Stats.MaximumHealth], Is.EqualTo(BossHealth * 4));
    }

    /// <summary>
    /// Tests that the result of a finished encounter is added to the history with its ranking, and that it's read again from the file.
    /// </summary>
    [Test]
    public async Task ResultIsAddedToHistoryAsync()
    {
        var path = Path.Combine(Path.GetTempPath(), $"boss-history-{Guid.NewGuid():N}.jsonl");
        try
        {
            var history = new BossEncounterHistory(path);
            var gameContext = CreateGameContext();
            var context = new BossEncounterContext(gameContext, CreateDefinition(), history);
            var player = await CreatePlayerAsync(gameContext, new Point(105, 100)).ConfigureAwait(false);
            await context.StartAsync(Start).ConfigureAwait(false);
            var boss = context.Boss!;

            // The hit is tracked by the hit plugin, which is active in the game context of the tests.
            await boss.ReflectDamageAsync(player, (uint)boss.Health).ConfigureAwait(false);
            await context.TickAsync(Start.AddSeconds(1)).ConfigureAwait(false);

            var result = (await history.GetRecentAsync(10, NullLogger.Instance).ConfigureAwait(false)).Single();
            Assert.That(result.Name, Is.EqualTo("Test"));
            Assert.That(result.Outcome, Is.EqualTo(BossEncounterOutcome.Defeated));
            Assert.That(result.PhaseName, Is.EqualTo("Pilares"));
            Assert.That(result.Ranking.Select(r => (r.Name, r.Damage, r.WasRewarded)), Is.EqualTo(new[] { (player.Name, (long)BossHealth * 2, false) }));

            var reloaded = await new BossEncounterHistory(path).GetRecentAsync(10, NullLogger.Instance).ConfigureAwait(false);
            Assert.That(reloaded.Single().Outcome, Is.EqualTo(BossEncounterOutcome.Defeated));
            Assert.That(reloaded.Single().Ranking, Has.Count.EqualTo(1));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Tests that the snapshot of a running encounter tells its place, phase, health and players.
    /// </summary>
    [Test]
    public async Task SnapshotDescribesRunningEncounterAsync()
    {
        var (context, _) = await StartEncounterAsync().ConfigureAwait(false);

        var snapshot = context.GetSnapshot();

        Assert.That(snapshot.IsRunning, Is.True);
        Assert.That((snapshot.X, snapshot.Y), Is.EqualTo((100, 100)));
        Assert.That(snapshot.PhaseName, Is.EqualTo("Pilares"));
        Assert.That(snapshot.HealthPercent, Is.EqualTo(100));
        Assert.That(snapshot.PlayersInArena, Is.EqualTo(1));

        context.RequestStop();
        await context.TickAsync(Start.AddSeconds(1)).ConfigureAwait(false);
        Assert.That(context.GetSnapshot().IsRunning, Is.False);
    }

    private static async ValueTask<(BossEncounterContext Context, Player Player)> StartEncounterAsync(BossEncounterDefinition? definition = null)
    {
        var gameContext = CreateGameContext();
        var context = new BossEncounterContext(gameContext, definition ?? CreateDefinition());
        var player = await CreatePlayerAsync(gameContext, new Point(105, 100)).ConfigureAwait(false);
        Assert.That(await context.StartAsync(Start).ConfigureAwait(false), Is.True);
        return (context, player);
    }

    private static ItemDefinition AddItemDefinition(IGameContext gameContext)
    {
        var itemDefinition = new ItemDefinition { Id = Guid.NewGuid(), Group = 14, Number = 13, MaximumItemLevel = 15, Durability = 1 };
        gameContext.Configuration.Items.Add(itemDefinition);
        return itemDefinition;
    }

    /// <summary>
    /// Creates the drop generator of the monsters, which drops one item of level 0 and 500 zen.
    /// </summary>
    private static IDropGenerator CreateMonsterDropGenerator(ItemDefinition itemDefinition)
    {
        var monsterDrop = new TemporaryItem { Definition = itemDefinition };
        var generator = new Mock<IDropGenerator>();
        generator.Setup(g => g.GenerateItemDropsAsync(It.IsAny<DataModel.Configuration.MonsterDefinition>(), It.IsAny<int>(), It.IsAny<Player>()))
            .Returns(ValueTask.FromResult<(IEnumerable<Item> Items, uint? Money)>((new Item[] { monsterDrop }, 500)));
        return generator.Object;
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
        SpawnPoints = new List<BossEncounterPoint> { new() { MapNumber = 0, X = 100, Y = 100 } },
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

    private static async ValueTask<Monster> SpawnAsync(IGameContext gameContext, GameMap map, short number, Point point, SpawnTrigger trigger, INpcIntelligence? intelligence = null)
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

        var monster = new Monster(spawnArea, definition, map, NullDropGenerator.Instance, intelligence ?? new NullMonsterIntelligence(), gameContext.PlugInManager, gameContext.PathFinderPool);
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
        await ((GameContext)gameContext).AddPlayerAsync(player).ConfigureAwait(false);
        return player;
    }
}
