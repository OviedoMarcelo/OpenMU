// <copyright file="BossEncounterPhase.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.BossEncounters;

using MUnique.OpenMU.DataModel.Composition;
using MUnique.OpenMU.Interfaces;

/// <summary>
/// A phase of a boss encounter with its mechanics.
/// </summary>
/// <remarks>
/// A phase starts when the health of the boss falls to its <see cref="HealthThreshold"/>.
/// The phases don't go back: when the boss heals above the threshold, the phase stays.
/// The damage of the mechanics is a percentage of the maximum health of the hit player, so that
/// a mechanic which isn't handled hurts every character alike, regardless of its stats.
/// </remarks>
public class BossEncounterPhase
{
    /// <summary>
    /// Gets or sets the name of the phase.
    /// </summary>
    [Display(Name = "Nombre")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the health of the boss in percent, at which the phase starts.
    /// </summary>
    [Display(Name = "Empieza con vida (%)", Description = "La fase empieza cuando la vida del boss baja a este porcentaje. La primera fase usa 100.")]
    [Range(0, 100)]
    public int HealthThreshold { get; set; } = 100;

    /// <summary>
    /// Gets or sets the message which is shown to the players of the map when the phase starts.
    /// Placeholder: {0} = name of the encounter.
    /// </summary>
    [Display(Name = "Mensaje al empezar", Description = "Mensaje dorado para los jugadores del mapa. {0} = nombre del encuentro. Vacío = sin mensaje.")]
    public LocalizedString EnterMessage { get; set; }

    /// <summary>
    /// Gets or sets the multiplier of the damage of the boss during this phase, e.g. for an enrage.
    /// </summary>
    [Display(Name = "Multiplicador de daño del boss", Description = "1 = sin cambios; 2 = el doble (enrage). Se multiplica con el del encuentro.")]
    [Range(0.1, 100)]
    public double BossDamageMultiplier { get; set; } = 1;

    /// <summary>
    /// Gets or sets the objects, like pillars or crystals, which appear when the phase starts.
    /// </summary>
    [Display(Name = "Objetos", Description = "Pilares o cristales que aparecen al empezar la fase y que hay que destruir.")]
    [MemberOfAggregate]
    public ICollection<BossEncounterObjectSpawn> Objects { get; set; } = new List<BossEncounterObjectSpawn>();

    /// <summary>
    /// Gets or sets the percentage of damage which the boss receives less for each living object.
    /// </summary>
    [Display(Name = "Reducción de daño por objeto (%)", Description = "Cada objeto vivo hace que el boss reciba este porcentaje menos de daño.")]
    [Range(0, 100)]
    public int DamageReductionPerObject { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the boss is invulnerable while objects are alive (a shield).
    /// </summary>
    [Display(Name = "Escudo mientras haya objetos", Description = "El boss no recibe daño hasta que se destruyen todos los objetos de la fase.")]
    public bool IsBossInvulnerableWhileObjectsAlive { get; set; }

    /// <summary>
    /// Gets or sets the time in which all objects have to be destroyed. <see cref="TimeSpan.Zero"/> means no limit.
    /// </summary>
    [Display(Name = "Tiempo para destruir los objetos", Description = "Si no se destruyen todos a tiempo, se aplica la penalidad y reaparecen. 0 = sin límite.")]
    public TimeSpan ObjectsTimeLimit { get; set; }

    /// <summary>
    /// Gets or sets the maximum time between the destruction of the first and the last object.
    /// <see cref="TimeSpan.Zero"/> means no limit.
    /// </summary>
    [Display(Name = "Ventana entre objetos", Description = "Tiempo máximo entre la destrucción del primer y del último objeto, para obligar a coordinar. Si se pasa, se aplica la penalidad y reaparecen. 0 = sin ventana.")]
    public TimeSpan ObjectsKillWindow { get; set; }

    /// <summary>
    /// Gets or sets the damage in percent of the maximum health, which every player of the arena receives when the objects weren't destroyed in time.
    /// </summary>
    [Display(Name = "Penalidad: daño (%)", Description = "Daño a todos los jugadores de la arena, en porcentaje de su vida máxima.")]
    [Range(0, 100)]
    public int FailureDamagePercent { get; set; }

    /// <summary>
    /// Gets or sets the health in percent of its maximum health, which the boss heals when the objects weren't destroyed in time.
    /// </summary>
    [Display(Name = "Penalidad: curación del boss (%)", Description = "Porcentaje de su vida máxima que se cura el boss.")]
    [Range(0, 100)]
    public int FailureBossHealPercent { get; set; }

    /// <summary>
    /// Gets or sets the message which is shown when the objects weren't destroyed in time.
    /// Placeholder: {0} = name of the encounter.
    /// </summary>
    [Display(Name = "Mensaje de penalidad", Description = "{0} = nombre del encuentro.")]
    public LocalizedString FailureMessage { get; set; }

    /// <summary>
    /// Gets or sets the message which is shown when all objects have been destroyed.
    /// Placeholder: {0} = name of the encounter.
    /// </summary>
    [Display(Name = "Mensaje al destruir los objetos", Description = "{0} = nombre del encuentro.")]
    public LocalizedString ObjectsDestroyedMessage { get; set; }

    /// <summary>
    /// Gets or sets the interval of the pulse of each living object. <see cref="TimeSpan.Zero"/> means no pulse.
    /// </summary>
    [Display(Name = "Pulso de los objetos: intervalo", Description = "Cada cuánto cada objeto vivo daña a los jugadores cercanos. 0 = sin pulso.")]
    public TimeSpan PulseInterval { get; set; }

    /// <summary>
    /// Gets or sets the radius of the pulse around each object.
    /// </summary>
    [Display(Name = "Pulso de los objetos: radio")]
    [Range(0, 30)]
    public byte PulseRadius { get; set; }

    /// <summary>
    /// Gets or sets the damage of the pulse in percent of the maximum health of the hit player.
    /// </summary>
    [Display(Name = "Pulso de los objetos: daño (%)")]
    [Range(0, 100)]
    public int PulseDamagePercent { get; set; }

    /// <summary>
    /// Gets or sets the monsters which the boss summons.
    /// </summary>
    [Display(Name = "Adds", Description = "Monstruos que invoca el boss alrededor suyo. Solo se completan los que faltan.")]
    [MemberOfAggregate]
    public ICollection<BossEncounterSummon> Summons { get; set; } = new List<BossEncounterSummon>();

    /// <summary>
    /// Gets or sets the interval of the summons. <see cref="TimeSpan.Zero"/> means only once when the phase starts.
    /// </summary>
    [Display(Name = "Adds: intervalo", Description = "Cada cuánto vuelve a invocar los adds que faltan. 0 = solo al empezar la fase.")]
    public TimeSpan SummonInterval { get; set; }

    /// <summary>
    /// Gets or sets the interval of the telegraphed area attack. <see cref="TimeSpan.Zero"/> means no area attack.
    /// </summary>
    [Display(Name = "Ataque en área: intervalo", Description = "Cada cuánto marca jugadores (p. ej. meteoritos). 0 = sin ataque en área.")]
    public TimeSpan AreaAttackInterval { get; set; }

    /// <summary>
    /// Gets or sets the number of players which are marked by each area attack.
    /// </summary>
    [Display(Name = "Ataque en área: jugadores marcados")]
    [Range(1, 50)]
    public int AreaAttackTargets { get; set; } = 1;

    /// <summary>
    /// Gets or sets the radius of the area attack around the position of a marked player.
    /// </summary>
    [Display(Name = "Ataque en área: radio")]
    [Range(0, 30)]
    public byte AreaAttackRadius { get; set; } = 3;

    /// <summary>
    /// Gets or sets the time between the mark and the hit, in which the players can move away.
    /// </summary>
    [Display(Name = "Ataque en área: aviso", Description = "Tiempo entre la marca y el golpe, para que los jugadores se alejen.")]
    public TimeSpan AreaAttackDelay { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Gets or sets the damage of the area attack in percent of the maximum health of the hit player.
    /// </summary>
    [Display(Name = "Ataque en área: daño (%)")]
    [Range(0, 100)]
    public int AreaAttackDamagePercent { get; set; }

    /// <summary>
    /// Gets or sets the duration of the stun of the hit players. <see cref="TimeSpan.Zero"/> means no stun.
    /// </summary>
    [Display(Name = "Ataque en área: stun", Description = "0 = sin stun.")]
    public TimeSpan AreaAttackStunDuration { get; set; }

    /// <summary>
    /// Gets or sets the message which is shown to the players of the arena when players are marked.
    /// Placeholder: {0} = name of the encounter.
    /// </summary>
    [Display(Name = "Ataque en área: mensaje", Description = "Mensaje dorado para la arena. {0} = nombre del encuentro.")]
    public LocalizedString AreaAttackMessage { get; set; }

    /// <summary>
    /// Gets or sets the message which is shown to a marked player.
    /// </summary>
    [Display(Name = "Ataque en área: mensaje al marcado", Description = "Mensaje para el jugador marcado.")]
    public LocalizedString AreaAttackMarkedMessage { get; set; }

    /// <summary>
    /// Gets the index of the phase which runs at the specified health of the boss.
    /// </summary>
    /// <param name="healthThresholds">The health thresholds of the phases, in descending order.</param>
    /// <param name="healthPercent">The health of the boss in percent.</param>
    /// <param name="currentIndex">The index of the current phase, or -1 if no phase started yet.</param>
    /// <returns>The index of the phase; -1 if no phase started yet.</returns>
    /// <remarks>The phases don't go back, so the result is never lower than <paramref name="currentIndex"/>.</remarks>
    public static int GetPhaseIndex(IReadOnlyList<int> healthThresholds, double healthPercent, int currentIndex)
    {
        var index = currentIndex;
        for (var i = currentIndex + 1; i < healthThresholds.Count; i++)
        {
            if (healthPercent <= healthThresholds[i])
            {
                index = i;
            }
        }

        return index;
    }

    /// <inheritdoc />
    public override string ToString() => $"{this.Name} ({this.HealthThreshold} %)";
}
