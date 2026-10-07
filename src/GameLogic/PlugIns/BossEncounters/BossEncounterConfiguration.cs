// <copyright file="BossEncounterConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.BossEncounters;

using MUnique.OpenMU.DataModel.Composition;

/// <summary>
/// The configuration of the <see cref="BossEncounterPlugIn"/>.
/// </summary>
public class BossEncounterConfiguration
{
    /// <summary>
    /// The number of the monster "Illusion of Kundun 7".
    /// </summary>
    private const short KundunNumber = 275;

    /// <summary>
    /// The number of the map Kalima 7.
    /// </summary>
    private const short Kalima7Number = 36;

    /// <summary>
    /// The number of the monster "Necron 7", a monster of Kalima 7.
    /// </summary>
    private const short NecronNumber = 335;

    /// <summary>
    /// The number of the monster "Schriker 7", a monster of Kalima 7.
    /// </summary>
    private const short SchrikerNumber = 337;

    /// <summary>
    /// The number of the monster "Spider Eggs 1" of raklion, which is used as pillar. It doesn't move.
    /// </summary>
    private const short PillarNumber = 460;

    /// <summary>
    /// The number of the monster "Spider Eggs 2" of raklion, which is used as crystal. It doesn't move.
    /// </summary>
    private const short CrystalNumber = 461;

    /// <summary>
    /// Gets the default configuration, with Kundun Reborn in Kalima 7 as example.
    /// </summary>
    public static BossEncounterConfiguration Default => new()
    {
        Encounters = new List<BossEncounterDefinition> { CreateKundunReborn() },
    };

    /// <summary>
    /// Gets or sets the encounters.
    /// </summary>
    [Display(Name = "Encuentros", Description = "Cada encuentro define el boss, el mapa, el lugar, los horarios y las fases.")]
    [MemberOfAggregate]
    [ScaffoldColumn(true)]
    public ICollection<BossEncounterDefinition> Encounters { get; set; } = new List<BossEncounterDefinition>();

    private static BossEncounterDefinition CreateKundunReborn() => new()
    {
        Name = "Kundun Reborn",
        MonsterNumber = KundunNumber,
        HealthMultiplier = 3,
        MapNumber = Kalima7Number,
        SpawnPoints = new List<BossEncounterPoint> { new() { X = 26, Y = 76 } },
        ArenaRadius = 18,
        HideAutomaticSpawns = true,
        Schedule = new List<BossEncounterScheduleEntry>
        {
            new() { Day = BossEncounterDay.EveryDay, Time = new TimeOnly(15, 0) },
            new() { Day = BossEncounterDay.EveryDay, Time = new TimeOnly(21, 0) },
        },
        AnnouncementMinutes = new List<int> { 10, 5, 1 },
        SoftEnrageAfter = TimeSpan.FromMinutes(15),
        MaximumDuration = TimeSpan.FromMinutes(20),
        RetreatDamagePercent = 80,
        AnnouncementMessage = "{0} aparece en {1} ({3}, {4}) en {2} minuto(s).",
        StartMessage = "¡{0} apareció en {1} ({2}, {3})!",
        DefeatedMessage = "¡{1} derrotó a {0}!",
        RetreatMessage = "{0} se retiró. El encuentro falló.",
        Phases = new List<BossEncounterPhase>
        {
            new()
            {
                Name = "Pilares",
                HealthThreshold = 100,
                EnterMessage = "¡{0} invoca 4 pilares! Destruilos para debilitarlo.",
                Objects = new List<BossEncounterObjectSpawn>
                {
                    new() { MonsterNumber = PillarNumber, X = 22, Y = 70, Health = 30000 },
                    new() { MonsterNumber = PillarNumber, X = 34, Y = 70, Health = 30000 },
                    new() { MonsterNumber = PillarNumber, X = 22, Y = 82, Health = 30000 },
                    new() { MonsterNumber = PillarNumber, X = 34, Y = 82, Health = 30000 },
                },
                DamageReductionPerObject = 20,
                ObjectsDestroyedMessage = "¡Los pilares cayeron! {0} es vulnerable.",
                PulseInterval = TimeSpan.FromSeconds(10),
                PulseRadius = 4,
                PulseDamagePercent = 10,
            },
            new()
            {
                Name = "Escudo",
                HealthThreshold = 75,
                EnterMessage = "¡{0} levanta un escudo! Destruí los 3 cristales a la vez.",
                Objects = new List<BossEncounterObjectSpawn>
                {
                    new() { MonsterNumber = CrystalNumber, X = 21, Y = 76, Health = 40000 },
                    new() { MonsterNumber = CrystalNumber, X = 40, Y = 70, Health = 40000 },
                    new() { MonsterNumber = CrystalNumber, X = 40, Y = 84, Health = 40000 },
                },
                IsBossInvulnerableWhileObjectsAlive = true,
                ObjectsTimeLimit = TimeSpan.FromSeconds(60),
                ObjectsKillWindow = TimeSpan.FromSeconds(10),
                FailureDamagePercent = 50,
                FailureBossHealPercent = 10,
                FailureMessage = "¡El escudo de {0} explotó! Los cristales vuelven a aparecer.",
                ObjectsDestroyedMessage = "¡El escudo de {0} cayó!",
            },
            new()
            {
                Name = "Meteoritos",
                HealthThreshold = 50,
                EnterMessage = "¡{0} invoca una lluvia de meteoritos! No se amontonen.",
                Summons = new List<BossEncounterSummon>
                {
                    new() { MonsterNumber = SchrikerNumber, Quantity = 4 },
                    new() { MonsterNumber = NecronNumber, Quantity = 2 },
                },
                SummonInterval = TimeSpan.FromSeconds(45),
                AreaAttackInterval = TimeSpan.FromSeconds(8),
                AreaAttackTargets = 3,
                AreaAttackRadius = 3,
                AreaAttackDelay = TimeSpan.FromSeconds(3),
                AreaAttackDamagePercent = 40,
                AreaAttackStunDuration = TimeSpan.FromSeconds(1),
                AreaAttackMessage = "¡Meteoritos! Los marcados, aléjense del grupo.",
                AreaAttackMarkedMessage = "¡Un meteorito cae sobre vos! Alejate del grupo.",
            },
            new()
            {
                Name = "Enrage",
                HealthThreshold = 20,
                EnterMessage = "¡{0} entra en furia!",
                BossDamageMultiplier = 2,
                AreaAttackInterval = TimeSpan.FromSeconds(4),
                AreaAttackTargets = 3,
                AreaAttackRadius = 3,
                AreaAttackDelay = TimeSpan.FromSeconds(3),
                AreaAttackDamagePercent = 40,
                AreaAttackStunDuration = TimeSpan.FromSeconds(1),
                AreaAttackMessage = "¡Meteoritos! Los marcados, aléjense del grupo.",
                AreaAttackMarkedMessage = "¡Un meteorito cae sobre vos! Alejate del grupo.",
            },
        },
    };
}
