// <copyright file="BossEncounterContext.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.BossEncounters;

using System.Collections.Concurrent;
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
    /// The number of players of the ranking of the damage, which are kept in the history.
    /// </summary>
    private const int MaximumHistoryRankingSize = 10;

    /// <summary>
    /// The center of a map. Together with <see cref="WholeMapRange"/>, it covers the whole map.
    /// </summary>
    private static readonly Point MapCenter = new(128, 128);

    /// <summary>
    /// The running encounters of all game servers, so that a heal finds the arena of its target without searching the plugins.
    /// </summary>
    private static readonly ConcurrentDictionary<BossEncounterContext, byte> RunningEncounters = new();

    private readonly IGameContext _gameContext;
    private readonly ILogger<BossEncounterContext> _logger;
    private readonly List<Monster> _objects = new();
    private readonly List<Monster> _summons = new();
    private readonly List<(Point Target, DateTime DueUtc, BossEncounterPhase Phase)> _pendingAreaAttacks = new();
    private readonly HashSet<Monster> _hiddenMonsters = new();
    private readonly HashSet<int> _announcedMinutes = new();
    private readonly HashSet<int> _announcedHealthPercents = new();
    private readonly HashSet<Player> _playersWithHealthBar = new();
    private readonly SimpleElement _shieldElement = new(0, AggregateType.Multiplicate);
    private readonly BossEncounterDamageTracker _damageTracker = new();
    private readonly BossEncounterHistory? _history;

    private BossEncounterDefinition _definition;
    private IReadOnlyList<BossEncounterPhase> _phases = [];
    private DateTime? _nextStart;
    private (GameMap Map, Point Point)? _plannedSpawnPlace;
    private GameMap? _map;
    private Monster? _boss;
    private DateTime _startUtc;
    private DateTime _enrageTimerStartUtc;
    private Point _arenaCenter;
    private DateTime? _arenaEmptySinceUtc;
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
    private SimpleElement? _healthScalingElement;
    private double _phaseDamageMultiplier = 1;
    private int _scalingPlayers = 1;
    private int _shownHealthPercent = -1;
    private int _shownPhaseIndex = -1;
    private DateTime _nextScalingUtc;
    private bool _isShieldActive;
    private int _isStartRequested;
    private int _isStopRequested;

    /// <summary>
    /// Initializes a new instance of the <see cref="BossEncounterContext"/> class.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <param name="definition">The definition of the encounter.</param>
    /// <param name="history">The history to which the results are added; <c>null</c> for none.</param>
    public BossEncounterContext(IGameContext gameContext, BossEncounterDefinition definition, BossEncounterHistory? history = null)
    {
        this._gameContext = gameContext;
        this._definition = definition;
        this._history = history;
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
    /// Gets the damage which the players did to the boss.
    /// </summary>
    public BossEncounterDamageTracker DamageTracker => this._damageTracker;

    /// <summary>
    /// Gets a value indicating whether the healing of the players in the arena is tracked.
    /// </summary>
    public bool IsHealingTracked => this._definition.IsHealingTracked;

    /// <summary>
    /// Gets the running encounter in whose arena the player is.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The encounter; <c>null</c>, if the player isn't in an arena.</returns>
    public static BossEncounterContext? FindArenaOf(Player player)
    {
        foreach (var encounter in RunningEncounters.Keys)
        {
            if (encounter.IsInArena(player))
            {
                return encounter;
            }
        }

        return null;
    }

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
            this._plannedSpawnPlace = null;
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
                await this.EndAsync(null, BossEncounterOutcome.Stopped).ConfigureAwait(false);
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
        var spawnPlace = this._plannedSpawnPlace;
        this._plannedSpawnPlace = null;

        var definition = this._definition;
        if (this.GetMonsterDefinition(definition.MonsterNumber) is not { } monsterDefinition)
        {
            this._logger.LogWarning("The boss encounter {Name} can't start, because the monster {Number} doesn't exist.", definition.Name, definition.MonsterNumber);
            return false;
        }

        spawnPlace ??= await this.ChooseSpawnPlaceAsync().ConfigureAwait(false);
        if (spawnPlace is not { } place)
        {
            this._logger.LogWarning("The boss encounter {Name} can't start, because none of its places of appearance is valid.", definition.Name);
            return false;
        }

        var (map, point) = place;
        this._map = map;
        this._phases = definition.GetOrderedPhases();
        this._phaseIndex = -1;
        this._isSoftEnraged = false;
        this._startUtc = utcNow;
        this._enrageTimerStartUtc = utcNow;
        this._arenaCenter = point;
        this._arenaEmptySinceUtc = null;
        this._announcedHealthPercents.Clear();
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

        if (await this.SpawnMonsterAsync(monsterDefinition, point, maximumHealth, new BasicMonsterIntelligence(), definition.GetBossAppearance(), new BossEncounterDropGenerator(this._gameContext.DropGenerator, definition, this._gameContext)).ConfigureAwait(false) is not { } boss)
        {
            this.IsRunning = false;
            await this.RestoreHiddenSpawnsAsync().ConfigureAwait(false);
            return false;
        }

        this._boss = boss;
        this._damageTracker.Clear();
        this._damageTracker.Track(boss);
        RunningEncounters[this] = 0;
        this._damageElement = null;
        this._damageReductionElement = null;
        this._healthScalingElement = null;
        this._scalingPlayers = 1;
        this._nextScalingUtc = utcNow;
        this._isShieldActive = false;
        this.UpdateBossDamage(1);
        this._logger.LogInformation("Boss encounter {Name} started at {Map} {Point}.", definition.Name, map.Definition.Name, point);
        await this.ShowGlobalMessageAsync(definition.StartMessage, true, definition.Name, GetMapName(map), point.X, point.Y).ConfigureAwait(false);
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

    /// <summary>
    /// Gets a value indicating whether the player is in the arena of the running encounter.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns><c>true</c>, if the encounter runs and the player is in its arena.</returns>
    public bool IsInArena(Player player)
    {
        return this.IsRunning
            && this._map is { } map
            && player.CurrentMap == map
            && player.IsInRange(this._arenaCenter, this._definition.ArenaRadius);
    }

    /// <summary>
    /// Gets the state of the encounter, e.g. for the admin panel.
    /// </summary>
    /// <returns>The state.</returns>
    public BossEncounterSnapshot GetSnapshot()
    {
        var boss = this._boss;
        var isRunning = this.IsRunning && boss is not null;
        return new BossEncounterSnapshot(
            this.Name,
            this._definition.IsActive,
            isRunning,
            this._nextStart,
            isRunning ? this._startUtc : null,
            isRunning && this._map is { } map ? GetMapName(map) : null,
            isRunning ? this._arenaCenter.X : null,
            isRunning ? this._arenaCenter.Y : null,
            isRunning ? this.CurrentPhase?.Name : null,
            boss is not null && isRunning ? Math.Round(GetHealthPercentage(boss), 1) : null,
            isRunning ? this.GetPlayersInArena().Count : 0);
    }

    private static double GetHealthPercentage(Monster monster)
    {
        var maximumHealth = monster.Attributes[Stats.MaximumHealth];
        return maximumHealth > 0 ? monster.Health * 100.0 / maximumHealth : 0;
    }

    private static string GetMapName(GameMap map)
    {
        return map.Definition.Name.ToString() ?? map.Definition.Number.ToString(CultureInfo.InvariantCulture);
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

    /// <summary>
    /// Gets a value indicating whether the player is still in the game with its character, even when it's dead.
    /// </summary>
    private static bool IsInWorld(Player player)
    {
        var state = player.PlayerState.CurrentState;
        return state == PlayerState.EnteredWorld || state == PlayerState.Dead || state == PlayerState.ChangingMap;
    }

    /// <summary>
    /// Gets the players of a ranking, the best one first. Players without damage or healing aren't in it.
    /// </summary>
    private static IReadOnlyList<BossEncounterParticipant> GetRanking(IReadOnlyList<BossEncounterParticipant> participants, BossEncounterRanking ranking)
    {
        return ranking == BossEncounterRanking.Healing
            ? participants.Where(p => p.Healing > 0).OrderByDescending(p => p.Healing).ToList()
            : participants.Where(p => p.Damage > 0).OrderByDescending(p => p.Damage).ToList();
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
        this._plannedSpawnPlace = null;
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
        // The place is chosen now, so that the announced map and coordinates are the ones where the boss appears.
        this._plannedSpawnPlace ??= await this.ChooseSpawnPlaceAsync().ConfigureAwait(false);
        if (this._plannedSpawnPlace is not { } place)
        {
            return;
        }

        var (map, point) = place;

        await this.ShowGlobalMessageAsync(this._definition.AnnouncementMessage, false, this._definition.Name, GetMapName(map), minutes, point.X, point.Y).ConfigureAwait(false);
    }

    private async ValueTask RunningTickAsync(DateTime utcNow)
    {
        if (this._boss is not { } boss)
        {
            await this.EndAsync(null, BossEncounterOutcome.Stopped).ConfigureAwait(false);
            return;
        }

        if (!boss.IsAlive)
        {
            // When the boss was removed otherwise (e.g. by a game master), it has no death information.
            var outcome = boss.LastDeath is null ? BossEncounterOutcome.Stopped : BossEncounterOutcome.Defeated;
            await this.EndAsync(boss.LastDeath, outcome).ConfigureAwait(false);
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

        if (await this.ResetIfArenaIsEmptyAsync(boss, utcNow).ConfigureAwait(false))
        {
            return;
        }

        await this.LeashAsync(boss).ConfigureAwait(false);
        this.UpdateScaling(boss, utcNow);

        if (this._definition.SoftEnrageAfter > TimeSpan.Zero && utcNow - this._enrageTimerStartUtc >= this._definition.SoftEnrageAfter)
        {
            this._isSoftEnraged = true;
        }

        await this.UpdatePhaseAsync(utcNow).ConfigureAwait(false);
        if (this.CurrentPhase is { } phase)
        {
            await this.RunPhaseAsync(boss, phase, utcNow).ConfigureAwait(false);
        }

        await this.ExecuteDueAreaAttacksAsync(boss, utcNow).ConfigureAwait(false);
        await this.UpdateHealthBarAsync(boss).ConfigureAwait(false);
        await this.AnnounceHealthAsync(boss).ConfigureAwait(false);
    }

    /// <summary>
    /// Shows the bar of the health of the boss to the players in the arena, when it changed or when they entered
    /// the arena, and hides it for the players who left.
    /// </summary>
    private async ValueTask UpdateHealthBarAsync(Monster boss)
    {
        var healthPercent = (int)Math.Clamp(Math.Ceiling(GetHealthPercentage(boss)), 0, 100);
        var hasChanged = healthPercent != this._shownHealthPercent || this._phaseIndex != this._shownPhaseIndex;
        this._shownHealthPercent = healthPercent;
        this._shownPhaseIndex = this._phaseIndex;

        var players = this.GetPlayersNearArena();
        var bossName = string.IsNullOrWhiteSpace(this._definition.DisplayName) ? this.Name : this._definition.DisplayName.Trim();
        var phaseName = this.CurrentPhase?.Name ?? string.Empty;
        foreach (var player in players)
        {
            if (this._playersWithHealthBar.Add(player) || hasChanged)
            {
                await player.InvokeViewPlugInAsync<IShowBossHealthPlugIn>(p => p.ShowBossHealthAsync(boss, bossName, phaseName, (byte)healthPercent)).ConfigureAwait(false);
            }
        }

        foreach (var player in this._playersWithHealthBar.Except(players).ToList())
        {
            this._playersWithHealthBar.Remove(player);
            await player.InvokeViewPlugInAsync<IShowBossHealthPlugIn>(p => p.HideBossHealthAsync(boss)).ConfigureAwait(false);
        }
    }

    private async ValueTask HideHealthBarAsync(Monster boss)
    {
        foreach (var player in this._playersWithHealthBar)
        {
            await player.InvokeViewPlugInAsync<IShowBossHealthPlugIn>(p => p.HideBossHealthAsync(boss)).ConfigureAwait(false);
        }

        this._playersWithHealthBar.Clear();
        this._shownHealthPercent = -1;
        this._shownPhaseIndex = -1;
    }

    /// <summary>
    /// Tells the players of the map when the health of the boss falls to one of the configured percentages, once each.
    /// </summary>
    private async ValueTask AnnounceHealthAsync(Monster boss)
    {
        var healthPercent = GetHealthPercentage(boss);
        var reached = this._definition.HealthAnnouncements
            .Where(percent => percent > 0 && percent < 100 && healthPercent <= percent && !this._announcedHealthPercents.Contains(percent))
            .ToList();
        if (reached.Count == 0)
        {
            return;
        }

        // When the health fell over several percentages at once, only the lowest one is shown.
        this._announcedHealthPercents.UnionWith(reached);
        await this.ShowMapMessageAsync(this._definition.HealthAnnouncementMessage, this.Name, reached.Min()).ConfigureAwait(false);
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
        foreach (var player in this.GetPlayersInArena())
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
        var players = this.GetPlayersInArena();
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
            var targetPosition = target.Position;
            this._pendingAreaAttacks.Add((targetPosition, utcNow + phase.AreaAttackDelay, phase));
            await this.ForEachPlayerNearArenaAsync<IShowGroundMarkerPlugIn>(p => p.ShowGroundWarningAsync(targetPosition, phase.AreaAttackRadius, phase.AreaAttackDelay)).ConfigureAwait(false);
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
            await this.ForEachPlayerNearArenaAsync<IShowGroundMarkerPlugIn>(p => p.ShowGroundHitAsync(attack.Target, attack.Phase.AreaAttackRadius)).ConfigureAwait(false);
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
            foreach (var player in this.GetPlayersInArena())
            {
                await this.DamagePlayerAsync(boss, player, this._definition.RetreatDamagePercent).ConfigureAwait(false);
            }
        }

        await this.EndAsync(null, BossEncounterOutcome.Retreated).ConfigureAwait(false);
    }

    /// <summary>
    /// Ends the encounter and removes its monsters.
    /// </summary>
    /// <param name="bossDeath">The death information of the boss, if it was defeated.</param>
    /// <param name="outcome">How the encounter ended, for the history.</param>
    private async ValueTask EndAsync(DeathInformation? bossDeath, BossEncounterOutcome outcome)
    {
        var boss = this._boss;
        this._boss = null;
        this.IsRunning = false;
        RunningEncounters.TryRemove(this, out _);
        var phaseName = this.CurrentPhase?.Name;
        var participants = this._damageTracker.GetParticipants();
        this._damageTracker.Clear();
        if (boss is not null)
        {
            this._damageTracker.Untrack(boss);
            await this.HideHealthBarAsync(boss).ConfigureAwait(false);
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

        this._logger.LogInformation("Boss encounter {Name} ended: {Outcome}.", this.Name, outcome);
        var rewardedPlayers = new HashSet<Player>();
        if (bossDeath is not null)
        {
            await this.ShowGlobalMessageAsync(this._definition.DefeatedMessage, true, this.Name, bossDeath.KillerName).ConfigureAwait(false);
            rewardedPlayers = await this.RewardParticipantsAsync(participants).ConfigureAwait(false);
            await this.GiveRankRewardsAsync(participants, rewardedPlayers).ConfigureAwait(false);
        }
        else
        {
            await this.ShowGlobalMessageAsync(this._definition.RetreatMessage, true, this.Name).ConfigureAwait(false);
        }

        await this.ShowRankingAsync(participants).ConfigureAwait(false);
        await this.AddToHistoryAsync(outcome, bossDeath, phaseName, participants, rewardedPlayers).ConfigureAwait(false);
        await this.NotifyEndedAsync(bossDeath is not null, participants).ConfigureAwait(false);
    }

    /// <summary>
    /// Tells the plugins (e.g. the quests) which players took part: the ones with at least the minimum share of
    /// the damage, or of the healing, so that healers count, too.
    /// </summary>
    private async ValueTask NotifyEndedAsync(bool isDefeated, IReadOnlyList<BossEncounterParticipant> participants)
    {
        if (this._gameContext.PlugInManager.GetPlugInPoint<IBossEncounterEndedPlugIn>() is not { } plugInPoint)
        {
            return;
        }

        var minimum = this._definition.ParticipationMinimumDamagePercent;
        var players = participants
            .Where(p => (p.Damage > 0 && p.Percent >= minimum) || (p.Healing > 0 && p.HealingPercent >= minimum))
            .Select(p => p.Player)
            .Where(IsInWorld)
            .Distinct()
            .ToList();
        try
        {
            await plugInPoint.BossEncounterEndedAsync(this.Name, isDefeated, players).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error in a plugin after the boss encounter {Name} ended.", this.Name);
        }
    }

    private async ValueTask AddToHistoryAsync(BossEncounterOutcome outcome, DeathInformation? bossDeath, string? phaseName, IReadOnlyList<BossEncounterParticipant> participants, HashSet<Player> rewardedPlayers)
    {
        if (this._history is not { } history)
        {
            return;
        }

        // The best ones of both rankings, so that healers without damage are kept, too.
        var kept = GetRanking(participants, BossEncounterRanking.Damage).Take(MaximumHistoryRankingSize)
            .Union(GetRanking(participants, BossEncounterRanking.Healing).Take(MaximumHistoryRankingSize))
            .ToHashSet();
        var ranking = participants
            .Where(kept.Contains)
            .Select(p => new BossEncounterRankingEntry(p.Name, p.Damage, p.Percent, rewardedPlayers.Contains(p.Player), p.Healing))
            .ToList();
        var result = new BossEncounterResult(
            this.Name,
            (this._gameContext as IGameServerContext)?.Id,
            this._map is { } map ? GetMapName(map) : string.Empty,
            this._startUtc,
            DateTime.UtcNow - this._startUtc,
            outcome,
            bossDeath?.KillerName,
            phaseName,
            ranking);
        await history.AddAsync(result, this._logger).ConfigureAwait(false);
    }

    /// <summary>
    /// Gives the rewards for the participation to the players with enough damage, who are still online.
    /// </summary>
    /// <returns>The players who got the reward.</returns>
    private async ValueTask<HashSet<Player>> RewardParticipantsAsync(IReadOnlyList<BossEncounterParticipant> participants)
    {
        var rewardedPlayers = new HashSet<Player>();
        var definition = this._definition;
        if (definition.ParticipationRewards.Count == 0 && definition.ParticipationMoney <= 0)
        {
            return rewardedPlayers;
        }

        foreach (var participant in participants.Where(p => p.Damage > 0 && p.Percent >= definition.ParticipationMinimumDamagePercent && IsInWorld(p.Player)))
        {
            try
            {
                var items = BossEncounterDropGenerator.GenerateItems(this._gameContext, definition.ParticipationRewards).ToList();
                await BossEncounterRewarder.GiveAsync(participant.Player, items, definition.ParticipationMoney).ConfigureAwait(false);
                rewardedPlayers.Add(participant.Player);
                await this.ShowMessageAsync(participant.Player, definition.ParticipationMessage, this.Name, participant.Percent).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Couldn't give the reward of the boss encounter {Name} to {Player}.", this.Name, participant.Player);
            }
        }

        return rewardedPlayers;
    }

    /// <summary>
    /// Gives the rewards for the places of the rankings to the players who are still online.
    /// A player at a place of several rewards gets all of them.
    /// </summary>
    private async ValueTask GiveRankRewardsAsync(IReadOnlyList<BossEncounterParticipant> participants, HashSet<Player> rewardedPlayers)
    {
        foreach (var rewardsOfRanking in this._definition.RankRewards.GroupBy(reward => reward.Ranking))
        {
            var ranking = GetRanking(participants, rewardsOfRanking.Key);
            var rankingName = rewardsOfRanking.Key == BossEncounterRanking.Healing ? "curación" : "daño";
            for (var index = 0; index < ranking.Count; index++)
            {
                var rank = index + 1;
                var player = ranking[index].Player;
                var rewards = rewardsOfRanking.Where(reward => reward.AppliesTo(rank)).ToList();
                if (rewards.Count == 0 || !IsInWorld(player))
                {
                    continue;
                }

                try
                {
                    var items = BossEncounterDropGenerator.GenerateItems(this._gameContext, rewards).ToList();
                    var money = (int)Math.Min(int.MaxValue, rewards.Sum(reward => (long)reward.Money));
                    await BossEncounterRewarder.GiveAsync(player, items, money).ConfigureAwait(false);
                    rewardedPlayers.Add(player);
                    await this.ShowMessageAsync(player, this._definition.RankRewardMessage, this.Name, rank, rankingName).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    this._logger.LogError(ex, "Couldn't give the reward of the place {Rank} of the boss encounter {Name} to {Player}.", rank, this.Name, player);
                }
            }
        }
    }

    /// <summary>
    /// Shows the players with the most damage in the chat of the players of the map.
    /// </summary>
    private async ValueTask ShowRankingAsync(IReadOnlyList<BossEncounterParticipant> participants)
    {
        var size = this._definition.RankingSize;
        if (size <= 0 || participants.Count == 0 || this._map is not { } map)
        {
            return;
        }

        var damageLines = GetRanking(participants, BossEncounterRanking.Damage)
            .Take(size)
            .Select((p, index) => string.Create(CultureInfo.InvariantCulture, $"{index + 1}. {p.Name}: {p.Percent:0.#} %"))
            .ToList();
        var healingLines = GetRanking(participants, BossEncounterRanking.Healing)
            .Take(size)
            .Select((p, index) => string.Create(CultureInfo.InvariantCulture, $"{index + 1}. {p.Name}: {p.HealingPercent:0.#} %"))
            .ToList();
        var players = await this._gameContext.GetPlayersAsync().ConfigureAwait(false);
        foreach (var player in players.Where(player => player.CurrentMap == map))
        {
            try
            {
                await this.ShowRankingLinesAsync(player, this._definition.RankingMessage, damageLines).ConfigureAwait(false);
                await this.ShowRankingLinesAsync(player, this._definition.HealingRankingMessage, healingLines).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Couldn't show the ranking of the boss encounter {Name} to {Player}.", this.Name, player);
            }
        }
    }

    private async ValueTask ShowRankingLinesAsync(Player player, LocalizedString titleMessage, IReadOnlyList<string> lines)
    {
        if (lines.Count == 0)
        {
            return;
        }

        var title = Format(titleMessage, player, [this.Name]);
        if (!string.IsNullOrEmpty(title))
        {
            await player.ShowBlueMessageAsync(title).ConfigureAwait(false);
        }

        foreach (var line in lines)
        {
            await player.ShowBlueMessageAsync(line).ConfigureAwait(false);
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

    /// <summary>
    /// Makes the boss stronger when more players are in the arena than before. It never gets weaker,
    /// so that players can't leave the arena to weaken it.
    /// </summary>
    private void UpdateScaling(Monster boss, DateTime utcNow)
    {
        var definition = this._definition;
        if ((definition.ScalingHealthPercentPerPlayer <= 0 && definition.ScalingDamagePercentPerPlayer <= 0) || utcNow < this._nextScalingUtc)
        {
            return;
        }

        this._nextScalingUtc = utcNow + (definition.ScalingInterval > TimeSpan.Zero ? definition.ScalingInterval : TimeSpan.FromSeconds(1));
        var players = Math.Min(this.GetPlayersInArena().Count, Math.Max(1, definition.ScalingMaximumPlayers));
        if (players <= this._scalingPlayers)
        {
            return;
        }

        this._scalingPlayers = players;
        this._logger.LogInformation("Boss encounter {Name} scales to {Players} players.", this.Name, players);
        this.UpdateHealthScaling(boss);
        this.UpdateBossDamage(this._phaseDamageMultiplier);
    }

    private double GetScalingFactor(int percentPerPlayer) => 1 + ((this._scalingPlayers - 1) * percentPerPlayer / 100.0);

    /// <summary>
    /// Sets the maximum health of the boss for the counted players, keeping the percentage of its current health.
    /// </summary>
    private void UpdateHealthScaling(Monster boss)
    {
        var factor = this.GetScalingFactor(this._definition.ScalingHealthPercentPerPlayer);
        if (factor <= 1)
        {
            return;
        }

        var healthRatio = (double)boss.Health / boss.Attributes[Stats.MaximumHealth];
        if (this._healthScalingElement is { } previous)
        {
            boss.Attributes.RemoveElement(previous, Stats.MaximumHealth);
        }

        this._healthScalingElement = new SimpleElement((float)factor, AggregateType.Multiplicate);
        boss.Attributes.AddElement(this._healthScalingElement, Stats.MaximumHealth);
        boss.Health = (int)Math.Min(int.MaxValue, boss.Attributes[Stats.MaximumHealth] * healthRatio);
    }

    private void UpdateBossDamage(double phaseMultiplier)
    {
        if (this._boss is not { } boss)
        {
            return;
        }

        this._phaseDamageMultiplier = phaseMultiplier;
        if (this._damageElement is { } previous)
        {
            boss.Attributes.RemoveElement(previous, Stats.AttackDamageIncrease);
            this._damageElement = null;
        }

        var multiplier = this._definition.DamageMultiplier * phaseMultiplier * this.GetScalingFactor(this._definition.ScalingDamagePercentPerPlayer);
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

    /// <summary>
    /// Gets the players in the arena, which is around the place where the boss appeared.
    /// </summary>
    private IReadOnlyList<Player> GetPlayersInArena() => this.GetPlayersInRange(this._arenaCenter, this._definition.ArenaRadius);

    private async ValueTask ForEachPlayerNearArenaAsync<TViewPlugIn>(Func<TViewPlugIn, ValueTask> action)
        where TViewPlugIn : class, IViewPlugIn
    {
        foreach (var player in this.GetPlayersNearArena())
        {
            await player.InvokeViewPlugInAsync(action).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Gets the players in the arena, the dead ones too, e.g. for the bar of the health of the boss.
    /// </summary>
    private IReadOnlyList<Player> GetPlayersNearArena()
    {
        if (this._map is not { } map)
        {
            return [];
        }

        var radius = this._definition.ArenaRadius;
        return map.GetAttackablesInRange(this._arenaCenter, radius)
            .OfType<Player>()
            .Where(player => player.IsInRange(this._arenaCenter, radius))
            .ToList();
    }

    /// <summary>
    /// Starts the encounter again, when there was no living player in the arena for the configured time,
    /// e.g. after all of them died. Nothing happens while the boss is untouched in its first phase.
    /// </summary>
    /// <returns><c>true</c>, if the encounter started again.</returns>
    private async ValueTask<bool> ResetIfArenaIsEmptyAsync(Monster boss, DateTime utcNow)
    {
        var resetAfter = this._definition.ResetAfterEmptyArena;
        if (resetAfter <= TimeSpan.Zero || this.GetPlayersInArena().Count > 0)
        {
            this._arenaEmptySinceUtc = null;
            return false;
        }

        this._arenaEmptySinceUtc ??= utcNow;
        var isUntouched = this._phaseIndex <= 0 && !this._isSoftEnraged && boss.Health >= (int)boss.Attributes[Stats.MaximumHealth];
        if (utcNow - this._arenaEmptySinceUtc < resetAfter || isUntouched)
        {
            return false;
        }

        await this.ResetAsync(boss, utcNow).ConfigureAwait(false);
        return true;
    }

    private async ValueTask ResetAsync(Monster boss, DateTime utcNow)
    {
        this._logger.LogInformation("Boss encounter {Name} starts again, because its arena was empty.", this.Name);
        this._arenaEmptySinceUtc = null;
        await this.RemoveObjectsAsync().ConfigureAwait(false);
        foreach (var monster in this._summons)
        {
            await RemoveMonsterAsync(monster).ConfigureAwait(false);
        }

        this._summons.Clear();
        this._pendingAreaAttacks.Clear();
        this._isSoftEnraged = false;
        this._enrageTimerStartUtc = utcNow;
        this._phaseIndex = -1;
        this._damageTracker.Clear();
        this._announcedHealthPercents.Clear();
        boss.Health = (int)boss.Attributes[Stats.MaximumHealth];
        this.UpdateBossDamage(1);
        await this.ShowMapMessageAsync(this._definition.ResetMessage, this.Name).ConfigureAwait(false);
        await this.UpdatePhaseAsync(utcNow).ConfigureAwait(false);
    }

    /// <summary>
    /// Teleports the boss back to the place where it appeared, when it's too far away.
    /// </summary>
    private async ValueTask LeashAsync(Monster boss)
    {
        var radius = this._definition.LeashRadius;
        if (radius == 0 || boss.IsInRange(this._arenaCenter, radius))
        {
            return;
        }

        await boss.StopWalkingAsync().ConfigureAwait(false);
        await boss.TeleportAsync(this._arenaCenter).ConfigureAwait(false);
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

    /// <summary>
    /// Chooses one of the places of appearance randomly.
    /// </summary>
    /// <returns>The map and the point; <c>null</c>, if the place isn't valid.</returns>
    private async ValueTask<(GameMap Map, Point Point)?> ChooseSpawnPlaceAsync()
    {
        var places = this._definition.SpawnPoints.ToList();
        if (places.Count == 0)
        {
            this._logger.LogWarning("The boss encounter {Name} has no place of appearance.", this.Name);
            return null;
        }

        var place = places[Rand.NextInt(0, places.Count)];
        if (await this._gameContext.GetMapAsync((ushort)place.MapNumber).ConfigureAwait(false) is not { } map)
        {
            this._logger.LogWarning("The map {Number} of the boss encounter {Name} doesn't exist.", place.MapNumber, this.Name);
            return null;
        }

        var point = place.IsRandomPoint() ? map.Terrain.RandomWalkableCoordinate : new Point(place.X, place.Y);
        return point is { } p ? (map, p) : null;
    }

    private Point GetWalkablePoint(Point point)
    {
        if (this._map is not { } map || map.Terrain.WalkMap[point.X, point.Y])
        {
            return point;
        }

        return map.Terrain.GetRandomCoordinate(point, WalkablePointRadius);
    }

    private async ValueTask<Monster?> SpawnMonsterAsync(MonsterDefinition monsterDefinition, Point point, int? maximumHealth, INpcIntelligence intelligence, MonsterAppearance? appearance = null, IDropGenerator? dropGenerator = null)
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

        var monster = new Monster(spawnArea, monsterDefinition, map, dropGenerator ?? this._gameContext.DropGenerator, intelligence, this._gameContext.PlugInManager, this._gameContext.PathFinderPool);
        try
        {
            monster.Initialize();

            // The appearance is sent when the monster comes into view, so it's set before.
            MonsterAppearances.Set(monster, appearance);
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
