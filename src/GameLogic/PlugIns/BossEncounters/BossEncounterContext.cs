// <copyright file="BossEncounterContext.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.BossEncounters;

using System.Globalization;
using System.Threading;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.Views;
using MUnique.OpenMU.GameLogic.Views.World;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Pathfinding;

/// <summary>
/// The run of a boss encounter on a game server.
/// </summary>
/// <remarks>
/// It's ticked about once per second by the <see cref="BossEncounterPlugIn"/>, which makes sure that
/// the ticks of a game server don't overlap. Its run is:
/// <list type="number">
///   <item>The encounter is announced to all players some minutes before its start.</item>
///   <item>The boss appears; the automatic spawns of the same monster on the map are hidden.</item>
///   <item>The phases start as the health of the boss goes down, each with its mechanics.</item>
///   <item>The boss is defeated, or it retreats when the maximum duration is reached.</item>
/// </list>
/// </remarks>
public sealed class BossEncounterContext
{
    /// <summary>
    /// The range around <see cref="MapCenter"/> which covers the whole map.
    /// </summary>
    private const int WholeMapRange = byte.MaxValue;

    /// <summary>
    /// The radius around a configured point, in which a walkable point is searched when the configured one isn't walkable.
    /// </summary>
    private const byte WalkablePointRadius = 3;

    /// <summary>
    /// The radius around the boss, in which its summons appear.
    /// </summary>
    private const byte SummonRadius = 4;

    /// <summary>
    /// The center of a map. Together with <see cref="WholeMapRange"/>, it covers the whole map.
    /// </summary>
    private static readonly Point MapCenter = new(128, 128);

    private readonly IGameContext _gameContext;
    private readonly ILogger<BossEncounterContext> _logger;
    private readonly List<Monster> _objects = new();
    private readonly List<Monster> _summons = new();
    private readonly List<(Point Target, DateTime DueUtc, BossEncounterPhase Phase)> _pendingAreaAttacks = new();
    private readonly HashSet<Monster> _hiddenMonsters = new();
    private readonly HashSet<int> _announcedMinutes = new();
    private readonly SimpleElement _shieldElement = new(0, AggregateType.Multiplicate);

    private BossEncounterDefinition _definition;
    private IReadOnlyList<BossEncounterPhase> _phases = [];
    private DateTime? _nextStart;
    private Point? _plannedSpawnPoint;
    private GameMap? _map;
    private Monster? _boss;
    private DateTime _startUtc;
    private int _phaseIndex = -1;
    private bool _isSoftEnraged;
    private DateTime _objectsSpawnedUtc;
    private DateTime? _firstObjectDestroyedUtc;
    private bool _areObjectsDestroyed;
    private DateTime _nextPulseUtc;
    private DateTime _nextSummonUtc;
    private DateTime _nextAreaAttackUtc;
    private SimpleElement? _damageElement;
    private SimpleElement? _damageReductionElement;
    private bool _isShieldActive;
    private int _isStartRequested;
    private int _isStopRequested;

    /// <summary>
    /// Initializes a new instance of the <see cref="BossEncounterContext"/> class.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <param name="definition">The definition of the encounter.</param>
    public BossEncounterContext(IGameContext gameContext, BossEncounterDefinition definition)
    {
        this._gameContext = gameContext;
        this._definition = definition;
        this._logger = gameContext.LoggerFactory.CreateLogger<BossEncounterContext>();
    }

    /// <summary>
    /// Gets the name of the encounter.
    /// </summary>
    public string Name => this._definition.Name;

    /// <summary>
    /// Gets a value indicating whether the encounter is running.
    /// </summary>
    public bool IsRunning { get; private set; }

    /// <summary>
    /// Gets the next start of the encounter in the time zone of the server, if it's known.
    /// </summary>
    public DateTime? NextStart => this._nextStart;

    /// <summary>
    /// Gets the boss, while the encounter is running.
    /// </summary>
    public Monster? Boss => this._boss;

    /// <summary>
    /// Gets the current phase, while the encounter is running.
    /// </summary>
    public BossEncounterPhase? CurrentPhase => this._phaseIndex >= 0 && this._phaseIndex < this._phases.Count ? this._phases[this._phaseIndex] : null;

    /// <summary>
    /// Gets the objects of the current phase.
    /// </summary>
    public IReadOnlyList<Monster> Objects => this._objects;

    /// <summary>
    /// Gets the monsters which are summoned by the boss.
    /// </summary>
    public IReadOnlyList<Monster> Summons => this._summons;

    /// <summary>
    /// Gets the number of area attacks which hit soon.
    /// </summary>
    public int PendingAreaAttackCount => this._pendingAreaAttacks.Count;

    /// <summary>
    /// Updates the definition, e.g. after it has been changed in the admin panel.
    /// A running encounter keeps its phases until it ends.
    /// </summary>
    /// <param name="definition">The definition.</param>
    public void UpdateDefinition(BossEncounterDefinition definition)
    {
        if (ReferenceEquals(this._definition, definition))
        {
            return;
        }

        this._definition = definition;
        if (!this.IsRunning)
        {
            // The schedule may have changed.
            this._nextStart = null;
            this._plannedSpawnPoint = null;
        }
    }

    /// <summary>
    /// Requests the start of the encounter by the next tick, e.g. by a game master.
    /// </summary>
    public void RequestStart()
    {
        Interlocked.Exchange(ref this._isStartRequested, 1);
    }

    /// <summary>
    /// Requests the end of the running encounter by the next tick, e.g. by a game master. The boss retreats without its last attack.
    /// </summary>
    public void RequestStop()
    {
        Interlocked.Exchange(ref this._isStopRequested, 1);
    }

    /// <summary>
    /// Executes one step of the encounter.
    /// </summary>
    /// <param name="utcNow">The current time, in UTC.</param>
    public async ValueTask TickAsync(DateTime utcNow)
    {
        if (this.IsRunning)
        {
            Interlocked.Exchange(ref this._isStartRequested, 0);
            if (Interlocked.Exchange(ref this._isStopRequested, 0) != 0)
            {
                await this.EndAsync(null).ConfigureAwait(false);
                return;
            }

            await this.RunningTickAsync(utcNow).ConfigureAwait(false);
            return;
        }

        Interlocked.Exchange(ref this._isStopRequested, 0);
        if (Interlocked.Exchange(ref this._isStartRequested, 0) != 0)
        {
            await this.StartAsync(utcNow).ConfigureAwait(false);
            return;
        }

        if (!this._definition.IsActive)
        {
            return;
        }

        var serverNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, this._gameContext.ServerTimeZone);
        if (this._nextStart is null)
        {
            this.PlanNextStart(serverNow);
        }

        if (this._nextStart is not { } nextStart)
        {
            return;
        }

        foreach (var minutes in this._definition.AnnouncementMinutes.Where(m => m > 0).OrderByDescending(m => m))
        {
            if (serverNow >= nextStart.AddMinutes(-minutes) && serverNow < nextStart && this._announcedMinutes.Add(minutes))
            {
                await this.AnnounceAsync(minutes).ConfigureAwait(false);
            }
        }

        if (serverNow >= nextStart)
        {
            await this.StartAsync(utcNow).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Starts the encounter: the boss appears.
    /// </summary>
    /// <param name="utcNow">The current time, in UTC.</param>
    /// <returns><c>true</c>, if the boss appeared; otherwise, <c>false</c>.</returns>
    public async ValueTask<bool> StartAsync(DateTime utcNow)
    {
        if (this.IsRunning)
        {
            return false;
        }

        this._nextStart = null;
        var spawnPoint = this._plannedSpawnPoint;
        this._plannedSpawnPoint = null;

        var definition = this._definition;
        if (this.GetMonsterDefinition(definition.MonsterNumber) is not { } monsterDefinition)
        {
            this._logger.LogWarning("The boss encounter {Name} can't start, because the monster {Number} doesn't exist.", definition.Name, definition.MonsterNumber);
            return false;
        }

        if (await this._gameContext.GetMapAsync((ushort)definition.MapNumber).ConfigureAwait(false) is not { } map)
        {
            this._logger.LogWarning("The boss encounter {Name} can't start, because the map {Number} doesn't exist.", definition.Name, definition.MapNumber);
            return false;
        }

        spawnPoint ??= this.ChooseSpawnPoint(map);
        if (spawnPoint is not { } point)
        {
            this._logger.LogWarning("The boss encounter {Name} can't start, because the map {Number} has no walkable point.", definition.Name, definition.MapNumber);
            return false;
        }

        this._map = map;
        this._phases = definition.GetOrderedPhases();
        this._phaseIndex = -1;
        this._isSoftEnraged = false;
        this._startUtc = utcNow;
        this.IsRunning = true;
        if (definition.HideAutomaticSpawns)
        {
            await this.HideAutomaticSpawnsAsync().ConfigureAwait(false);
        }

        int? maximumHealth = null;
        if (Math.Abs(definition.HealthMultiplier - 1) > 0.001
            && monsterDefinition.Attributes.FirstOrDefault(a => a.AttributeDefinition == Stats.MaximumHealth) is { } healthAttribute)
        {
            maximumHealth = (int)Math.Min(int.MaxValue, healthAttribute.Value * definition.HealthMultiplier);
        }

        if (await this.SpawnMonsterAsync(monsterDefinition, point, maximumHealth, new BasicMonsterIntelligence()).ConfigureAwait(false) is not { } boss)
        {
            this.IsRunning = false;
            await this.RestoreHiddenSpawnsAsync().ConfigureAwait(false);
            return false;
        }

        this._boss = boss;
        this._damageElement = null;
        this._damageReductionElement = null;
        this._isShieldActive = false;
        this.UpdateBossDamage(1);
        this._logger.LogInformation("Boss encounter {Name} started at {Map} {Point}.", definition.Name, map.Definition.Name, point);
        await this.ShowGlobalMessageAsync(definition.StartMessage, true, definition.Name, this.GetMapName(), point.X, point.Y).ConfigureAwait(false);
        await this.UpdatePhaseAsync(utcNow).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Gets a short text about the state of the encounter, e.g. for a game master.
    /// </summary>
    /// <returns>The text.</returns>
    public string GetStatusText()
    {
        if (this.IsRunning)
        {
            var health = this._boss is { } boss ? GetHealthPercentage(boss) : 0;
            return $"{this.Name}: en curso, fase {this.CurrentPhase?.Name ?? "-"}, vida {health:0} %";
        }

        if (!this._definition.IsActive)
        {
            return $"{this.Name}: inactivo";
        }

        return this._nextStart is { } nextStart
            ? $"{this.Name}: próximo {nextStart.ToString("dd/MM HH:mm", CultureInfo.InvariantCulture)}"
            : $"{this.Name}: sin horarios";
    }

    private static double GetHealthPercentage(Monster monster)
    {
        var maximumHealth = monster.Attributes[Stats.MaximumHealth];
        return maximumHealth > 0 ? monster.Health * 100.0 / maximumHealth : 0;
    }

    private static string Format(LocalizedString template, Player player, object?[] arguments)
    {
        var text = template.GetTranslation(player.Culture) ?? string.Empty;
        try
        {
            return string.Format(player.Culture, text, arguments);
        }
        catch (FormatException)
        {
            // A misconfigured message shouldn't break the encounter.
            return text;
        }
    }

    private static async ValueTask RemoveMonsterAsync(Monster monster)
    {
        if (monster.IsAlive)
        {
            await monster.CurrentMap.RemoveAsync(monster).ConfigureAwait(false);
            monster.Dispose();
        }
    }

    private void PlanNextStart(DateTime serverNow)
    {
        // A start in this very second already happened, so the next one is searched from the next second.
        this._nextStart = this._definition.GetNextStart(serverNow.AddSeconds(1));
        this._plannedSpawnPoint = null;
        this._announcedMinutes.Clear();
        if (this._nextStart is not { } nextStart)
        {
            return;
        }

        // Announcements which would have been shown before now are skipped, e.g. after a restart of the server.
        foreach (var minutes in this._definition.AnnouncementMinutes.Where(m => serverNow > nextStart.AddMinutes(-m)))
        {
            this._announcedMinutes.Add(minutes);
        }
    }

    private async ValueTask AnnounceAsync(int minutes)
    {
        var definition = this._definition;
        if (await this._gameContext.GetMapAsync((ushort)definition.MapNumber).ConfigureAwait(false) is { } map)
        {
            // The point is chosen now, so that the announced coordinates are the ones where the boss appears.
            this._plannedSpawnPoint ??= this.ChooseSpawnPoint(map);
        }

        var point = this._plannedSpawnPoint ?? default;
        await this.ShowGlobalMessageAsync(definition.AnnouncementMessage, false, definition.Name, this.GetMapName(), minutes, point.X, point.Y).ConfigureAwait(false);
    }

    private async ValueTask RunningTickAsync(DateTime utcNow)
    {
        if (this._boss is not { } boss)
        {
            await this.EndAsync(null).ConfigureAwait(false);
            return;
        }

        if (!boss.IsAlive)
        {
            // When the boss was removed otherwise (e.g. by a game master), it has no death information.
            await this.EndAsync(boss.LastDeath).ConfigureAwait(false);
            return;
        }

        if (this._definition.HideAutomaticSpawns)
        {
            // A hidden monster may have died before, and respawned in the meantime.
            await this.HideAutomaticSpawnsAsync().ConfigureAwait(false);
        }

        var elapsed = utcNow - this._startUtc;
        if (this._definition.MaximumDuration > TimeSpan.Zero && elapsed >= this._definition.MaximumDuration)
        {
            await this.RetreatAsync().ConfigureAwait(false);
            return;
        }

        if (this._definition.SoftEnrageAfter > TimeSpan.Zero && elapsed >= this._definition.SoftEnrageAfter)
        {
            this._isSoftEnraged = true;
        }

        await this.UpdatePhaseAsync(utcNow).ConfigureAwait(false);
        if (this.CurrentPhase is { } phase)
        {
            await this.RunPhaseAsync(boss, phase, utcNow).ConfigureAwait(false);
        }

        await this.ExecuteDueAreaAttacksAsync(boss, utcNow).ConfigureAwait(false);
    }

    private async ValueTask UpdatePhaseAsync(DateTime utcNow)
    {
        if (this._boss is not { } boss || this._phases.Count == 0)
        {
            return;
        }

        var thresholds = this._phases.Select(phase => phase.HealthThreshold).ToList();
        var index = BossEncounterPhase.GetPhaseIndex(thresholds, GetHealthPercentage(boss), this._phaseIndex);
        if (this._isSoftEnraged)
        {
            index = this._phases.Count - 1;
        }

        if (index != this._phaseIndex)
        {
            await this.EnterPhaseAsync(boss, index, utcNow).ConfigureAwait(false);
        }
    }

    private async ValueTask EnterPhaseAsync(Monster boss, int index, DateTime utcNow)
    {
        await this.RemoveObjectsAsync().ConfigureAwait(false);
        this._phaseIndex = index;
        var phase = this._phases[index];
        this._logger.LogInformation("Boss encounter {Name} entered the phase {Phase}.", this.Name, phase.Name);
        this.UpdateBossDamage(phase.BossDamageMultiplier);
        await this.ShowMapMessageAsync(phase.EnterMessage, this.Name).ConfigureAwait(false);

        await this.SpawnObjectsAsync(phase, utcNow).ConfigureAwait(false);
        this._nextPulseUtc = utcNow + phase.PulseInterval;
        this._nextAreaAttackUtc = utcNow + phase.AreaAttackInterval;
        this._nextSummonUtc = utcNow + phase.SummonInterval;
        if (phase.Summons.Count > 0)
        {
            await this.SummonAsync(boss, phase).ConfigureAwait(false);
        }
    }

    private async ValueTask RunPhaseAsync(Monster boss, BossEncounterPhase phase, DateTime utcNow)
    {
        if (!this._areObjectsDestroyed)
        {
            var aliveObjects = this._objects.Count(o => o.IsAlive);
            if (aliveObjects == 0)
            {
                this._areObjectsDestroyed = true;
                this.UpdateObjectEffects(phase);
                await this.ShowMapMessageAsync(phase.ObjectsDestroyedMessage, this.Name).ConfigureAwait(false);
            }
            else
            {
                if (aliveObjects < this._objects.Count)
                {
                    this._firstObjectDestroyedUtc ??= utcNow;
                }

                var isTimeUp = phase.ObjectsTimeLimit > TimeSpan.Zero && utcNow - this._objectsSpawnedUtc >= phase.ObjectsTimeLimit;
                var isWindowMissed = phase.ObjectsKillWindow > TimeSpan.Zero && this._firstObjectDestroyedUtc is { } first && utcNow - first >= phase.ObjectsKillWindow;
                if (isTimeUp || isWindowMissed)
                {
                    await this.FailObjectsAsync(boss, phase, utcNow).ConfigureAwait(false);
                }
                else
                {
                    this.UpdateObjectEffects(phase);
                }
            }
        }

        if (phase.PulseInterval > TimeSpan.Zero && utcNow >= this._nextPulseUtc)
        {
            this._nextPulseUtc = utcNow + phase.PulseInterval;
            foreach (var pillar in this._objects.Where(o => o.IsAlive).ToList())
            {
                foreach (var player in this.GetPlayersInRange(pillar.Position, phase.PulseRadius))
                {
                    await this.DamagePlayerAsync(pillar, player, phase.PulseDamagePercent).ConfigureAwait(false);
                }
            }
        }

        if (phase.Summons.Count > 0 && phase.SummonInterval > TimeSpan.Zero && utcNow >= this._nextSummonUtc)
        {
            this._nextSummonUtc = utcNow + phase.SummonInterval;
            await this.SummonAsync(boss, phase).ConfigureAwait(false);
        }

        if (phase.AreaAttackInterval > TimeSpan.Zero && utcNow >= this._nextAreaAttackUtc)
        {
            this._nextAreaAttackUtc = utcNow + phase.AreaAttackInterval;
            await this.MarkAreaAttackTargetsAsync(boss, phase, utcNow).ConfigureAwait(false);
        }
    }

    private async ValueTask FailObjectsAsync(Monster boss, BossEncounterPhase phase, DateTime utcNow)
    {
        this._logger.LogInformation("Boss encounter {Name}: the objects of the phase {Phase} weren't destroyed in time.", this.Name, phase.Name);
        await this.ShowMapMessageAsync(phase.FailureMessage, this.Name).ConfigureAwait(false);
        foreach (var player in this.GetPlayersInRange(boss.Position, this._definition.ArenaRadius))
        {
            await this.DamagePlayerAsync(boss, player, phase.FailureDamagePercent).ConfigureAwait(false);
        }

        if (phase.FailureBossHealPercent > 0)
        {
            var maximumHealth = (int)boss.Attributes[Stats.MaximumHealth];
            boss.Health = Math.Min(maximumHealth, boss.Health + (maximumHealth * phase.FailureBossHealPercent / 100));
        }

        await this.RemoveObjectsAsync().ConfigureAwait(false);
        await this.SpawnObjectsAsync(phase, utcNow).ConfigureAwait(false);
    }

    private async ValueTask SpawnObjectsAsync(BossEncounterPhase phase, DateTime utcNow)
    {
        foreach (var spawn in phase.Objects)
        {
            if (this.GetMonsterDefinition(spawn.MonsterNumber) is not { } monsterDefinition)
            {
                this._logger.LogWarning("The object {Number} of the boss encounter {Name} doesn't exist.", spawn.MonsterNumber, this.Name);
                continue;
            }

            var point = this.GetWalkablePoint(new Point(spawn.X, spawn.Y));
            var health = spawn.Health > 0 ? spawn.Health : (int?)null;

            // The objects don't move and don't attack by themselves.
            if (await this.SpawnMonsterAsync(monsterDefinition, point, health, new NullMonsterIntelligence()).ConfigureAwait(false) is { } monster)
            {
                this._objects.Add(monster);
            }
        }

        this._objectsSpawnedUtc = utcNow;
        this._firstObjectDestroyedUtc = null;
        this._areObjectsDestroyed = this._objects.Count == 0;
        this.UpdateObjectEffects(phase);
    }

    private async ValueTask RemoveObjectsAsync()
    {
        foreach (var monster in this._objects)
        {
            await RemoveMonsterAsync(monster).ConfigureAwait(false);
        }

        this._objects.Clear();
        this._areObjectsDestroyed = true;
        this._firstObjectDestroyedUtc = null;
    }

    private async ValueTask SummonAsync(Monster boss, BossEncounterPhase phase)
    {
        this._summons.RemoveAll(monster => !monster.IsAlive);
        foreach (var summon in phase.Summons)
        {
            if (this.GetMonsterDefinition(summon.MonsterNumber) is not { } monsterDefinition)
            {
                this._logger.LogWarning("The summon {Number} of the boss encounter {Name} doesn't exist.", summon.MonsterNumber, this.Name);
                continue;
            }

            // Only the missing monsters are summoned, so that they don't pile up.
            var missing = summon.Quantity - this._summons.Count(monster => monster.Definition == monsterDefinition);
            for (var i = 0; i < missing; i++)
            {
                var point = boss.CurrentMap.Terrain.GetRandomCoordinate(boss.Position, SummonRadius);
                if (await this.SpawnMonsterAsync(monsterDefinition, point, null, new BasicMonsterIntelligence()).ConfigureAwait(false) is { } monster)
                {
                    this._summons.Add(monster);
                }
            }
        }
    }

    private async ValueTask MarkAreaAttackTargetsAsync(Monster boss, BossEncounterPhase phase, DateTime utcNow)
    {
        var players = this.GetPlayersInRange(boss.Position, this._definition.ArenaRadius);
        if (players.Count == 0)
        {
            return;
        }

        var targets = players.OrderBy(_ => Rand.NextInt(0, int.MaxValue)).Take(phase.AreaAttackTargets).ToList();
        foreach (var player in players.Except(targets))
        {
            await this.ShowMessageAsync(player, phase.AreaAttackMessage, this.Name).ConfigureAwait(false);
        }

        foreach (var target in targets)
        {
            this._pendingAreaAttacks.Add((target.Position, utcNow + phase.AreaAttackDelay, phase));
            await this.ShowMessageAsync(target, phase.AreaAttackMarkedMessage, this.Name).ConfigureAwait(false);
            await this.ShowSkillAnimationAsync(boss, target).ConfigureAwait(false);
        }
    }

    private async ValueTask ExecuteDueAreaAttacksAsync(Monster boss, DateTime utcNow)
    {
        var dueAttacks = this._pendingAreaAttacks.Where(attack => attack.DueUtc <= utcNow).ToList();
        foreach (var attack in dueAttacks)
        {
            this._pendingAreaAttacks.Remove(attack);
            foreach (var player in this.GetPlayersInRange(attack.Target, attack.Phase.AreaAttackRadius))
            {
                await this.ShowSkillAnimationAsync(boss, player).ConfigureAwait(false);
                await this.DamagePlayerAsync(boss, player, attack.Phase.AreaAttackDamagePercent).ConfigureAwait(false);
                if (player.IsAlive && attack.Phase.AreaAttackStunDuration > TimeSpan.Zero)
                {
                    await player.ApplyStunEffectAsync(attack.Phase.AreaAttackStunDuration).ConfigureAwait(false);
                }
            }
        }
    }

    private async ValueTask RetreatAsync()
    {
        if (this._boss is { } boss)
        {
            foreach (var player in this.GetPlayersInRange(boss.Position, this._definition.ArenaRadius))
            {
                await this.DamagePlayerAsync(boss, player, this._definition.RetreatDamagePercent).ConfigureAwait(false);
            }
        }

        await this.EndAsync(null).ConfigureAwait(false);
    }

    /// <summary>
    /// Ends the encounter and removes its monsters.
    /// </summary>
    /// <param name="bossDeath">The death information of the boss, if it was defeated.</param>
    private async ValueTask EndAsync(DeathInformation? bossDeath)
    {
        var boss = this._boss;
        this._boss = null;
        this.IsRunning = false;
        if (boss is not null)
        {
            await RemoveMonsterAsync(boss).ConfigureAwait(false);
        }

        await this.RemoveObjectsAsync().ConfigureAwait(false);
        foreach (var monster in this._summons)
        {
            await RemoveMonsterAsync(monster).ConfigureAwait(false);
        }

        this._summons.Clear();
        this._pendingAreaAttacks.Clear();
        this._phaseIndex = -1;
        this._nextStart = null;
        await this.RestoreHiddenSpawnsAsync().ConfigureAwait(false);

        this._logger.LogInformation("Boss encounter {Name} ended, defeated: {Defeated}.", this.Name, bossDeath is not null);
        if (bossDeath is not null)
        {
            await this.ShowGlobalMessageAsync(this._definition.DefeatedMessage, true, this.Name, bossDeath.KillerName).ConfigureAwait(false);
        }
        else
        {
            await this.ShowGlobalMessageAsync(this._definition.RetreatMessage, true, this.Name).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Hides the monsters of the boss which appear on the map by themselves, by removing them from the
    /// area of interest. They keep their state, and are shown again when the encounter ends.
    /// </summary>
    private async ValueTask HideAutomaticSpawnsAsync()
    {
        if (this._map is not { } map)
        {
            return;
        }

        var monsters = map.GetNpcsInRange(MapCenter, WholeMapRange)
            .OfType<Monster>()
            .Where(monster => monster != this._boss
                              && monster.IsAlive
                              && monster.Definition.Number == this._definition.MonsterNumber
                              && monster.SpawnArea.SpawnTrigger == SpawnTrigger.Automatic)
            .ToList();
        foreach (var monster in monsters)
        {
            if (this._hiddenMonsters.Add(monster))
            {
                await map.InitRespawnAsync(monster).ConfigureAwait(false);
            }
        }
    }

    private async ValueTask RestoreHiddenSpawnsAsync()
    {
        foreach (var monster in this._hiddenMonsters)
        {
            try
            {
                if (monster.IsAlive)
                {
                    await monster.CurrentMap.RespawnAsync(monster).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Couldn't show the hidden {Monster} again.", monster);
            }
        }

        this._hiddenMonsters.Clear();
    }

    /// <summary>
    /// Updates the damage reduction and the shield of the boss, which depend on the living objects.
    /// </summary>
    private void UpdateObjectEffects(BossEncounterPhase phase)
    {
        if (this._boss is not { } boss)
        {
            return;
        }

        var aliveObjects = this._areObjectsDestroyed ? 0 : this._objects.Count(o => o.IsAlive);
        var factor = Math.Max(0, 1 - (aliveObjects * phase.DamageReductionPerObject / 100.0));
        if (Math.Abs((this._damageReductionElement?.Value ?? 1) - factor) > 0.0001)
        {
            if (this._damageReductionElement is { } previous)
            {
                boss.Attributes.RemoveElement(previous, Stats.DamageReceiveDecrement);
                this._damageReductionElement = null;
            }

            if (factor < 1)
            {
                this._damageReductionElement = new SimpleElement((float)factor, AggregateType.Multiplicate);
                boss.Attributes.AddElement(this._damageReductionElement, Stats.DamageReceiveDecrement);
            }
        }

        var shouldShield = phase.IsBossInvulnerableWhileObjectsAlive && aliveObjects > 0;
        if (shouldShield != this._isShieldActive)
        {
            this._isShieldActive = shouldShield;
            if (shouldShield)
            {
                boss.Attributes.AddElement(this._shieldElement, Stats.DamageReceiveDecrement);
            }
            else
            {
                boss.Attributes.RemoveElement(this._shieldElement, Stats.DamageReceiveDecrement);
            }
        }
    }

    private void UpdateBossDamage(double phaseMultiplier)
    {
        if (this._boss is not { } boss)
        {
            return;
        }

        if (this._damageElement is { } previous)
        {
            boss.Attributes.RemoveElement(previous, Stats.AttackDamageIncrease);
            this._damageElement = null;
        }

        var multiplier = this._definition.DamageMultiplier * phaseMultiplier;
        if (Math.Abs(multiplier - 1) > 0.001)
        {
            this._damageElement = new SimpleElement((float)multiplier, AggregateType.Multiplicate);
            boss.Attributes.AddElement(this._damageElement, Stats.AttackDamageIncrease);
        }
    }

    /// <summary>
    /// Damages a player by a percentage of its maximum health. The shield absorbs a part of it, like with other hits.
    /// </summary>
    private async ValueTask DamagePlayerAsync(IAttacker attacker, Player player, int percent)
    {
        if (percent <= 0 || !player.IsAlive || player.Attributes is not { } attributes)
        {
            return;
        }

        var damage = (uint)Math.Max(1, attributes[Stats.MaximumHealth] * percent / 100.0);
        try
        {
            await player.ApplyBleedingDamageAsync(attacker, damage).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Couldn't damage {Player} by the boss encounter {Name}.", player, this.Name);
        }
    }

    private IReadOnlyList<Player> GetPlayersInRange(Point point, int range)
    {
        if (this._map is not { } map)
        {
            return [];
        }

        return map.GetAttackablesInRange(point, range)
            .OfType<Player>()
            .Where(player => player.IsActive() && !player.IsAtSafezone() && player.IsInRange(point, range))
            .ToList();
    }

    private MonsterDefinition? GetMonsterDefinition(short number)
    {
        return this._gameContext.Configuration.Monsters.FirstOrDefault(m => m.Number == number);
    }

    private string GetMapName()
    {
        var definition = this._map?.Definition
                         ?? this._gameContext.Configuration.Maps.FirstOrDefault(m => m.Number == this._definition.MapNumber);
        return definition?.Name.ToString() ?? this._definition.MapNumber.ToString(CultureInfo.InvariantCulture);
    }

    private Point? ChooseSpawnPoint(GameMap map)
    {
        var points = this._definition.SpawnPoints.ToList();
        if (points.Count == 0)
        {
            return map.Terrain.RandomWalkableCoordinate;
        }

        var point = points[Rand.NextInt(0, points.Count)];
        return new Point(point.X, point.Y);
    }

    private Point GetWalkablePoint(Point point)
    {
        if (this._map is not { } map || map.Terrain.WalkMap[point.X, point.Y])
        {
            return point;
        }

        return map.Terrain.GetRandomCoordinate(point, WalkablePointRadius);
    }

    private async ValueTask<Monster?> SpawnMonsterAsync(MonsterDefinition monsterDefinition, Point point, int? maximumHealth, INpcIntelligence intelligence)
    {
        var map = this._map!;
        var spawnArea = new MonsterSpawnArea
        {
            GameMap = map.Definition,
            MonsterDefinition = monsterDefinition,
            SpawnTrigger = SpawnTrigger.OnceAtEventStart,
            Quantity = 1,
            X1 = point.X,
            X2 = point.X,
            Y1 = point.Y,
            Y2 = point.Y,
            MaximumHealthOverride = maximumHealth,
        };

        var monster = new Monster(spawnArea, monsterDefinition, map, this._gameContext.DropGenerator, intelligence, this._gameContext.PlugInManager, this._gameContext.PathFinderPool);
        try
        {
            monster.Initialize();
            await map.AddAsync(monster).ConfigureAwait(false);
            monster.OnSpawn();
            return monster;
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Couldn't spawn {Monster} of the boss encounter {Name}.", monsterDefinition, this.Name);
            monster.Dispose();
            return null;
        }
    }

    private ValueTask ShowSkillAnimationAsync(Monster boss, Player target)
    {
        if (boss.Definition.AttackSkill is not { } skill)
        {
            return ValueTask.CompletedTask;
        }

        return boss.ForEachWorldObserverAsync<IShowSkillAnimationPlugIn>(p => p.ShowSkillAnimationAsync(boss, target, skill, true), true);
    }

    private async ValueTask ShowMessageAsync(Player player, LocalizedString message, params object?[] arguments)
    {
        if (string.IsNullOrEmpty(message.Value))
        {
            return;
        }

        try
        {
            var text = Format(message, player, arguments);
            await player.InvokeViewPlugInAsync<IShowMessagePlugIn>(p => p.ShowMessageAsync(text, MessageType.GoldenCenter)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Couldn't notify {Player} about the boss encounter {Name}.", player, this.Name);
        }
    }

    /// <summary>
    /// Shows a golden message to the players of the map of the encounter.
    /// </summary>
    private async ValueTask ShowMapMessageAsync(LocalizedString message, params object?[] arguments)
    {
        if (string.IsNullOrEmpty(message.Value) || this._map is not { } map)
        {
            return;
        }

        var players = await this._gameContext.GetPlayersAsync().ConfigureAwait(false);
        foreach (var player in players.Where(player => player.CurrentMap == map))
        {
            await this.ShowMessageAsync(player, message, arguments).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Shows a golden message to all players of the game server.
    /// </summary>
    private async ValueTask ShowGlobalMessageAsync(LocalizedString message, bool showInChat, params object?[] arguments)
    {
        if (string.IsNullOrEmpty(message.Value))
        {
            return;
        }

        var players = await this._gameContext.GetPlayersAsync().ConfigureAwait(false);
        foreach (var player in players)
        {
            await this.ShowMessageAsync(player, message, arguments).ConfigureAwait(false);
            if (showInChat)
            {
                try
                {
                    await player.ShowBlueMessageAsync(Format(message, player, arguments)).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    this._logger.LogError(ex, "Couldn't notify {Player} about the boss encounter {Name}.", player, this.Name);
                }
            }
        }
    }
}
