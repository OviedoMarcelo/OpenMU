// <copyright file="BossEncounterDefinition.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.BossEncounters;

using MUnique.OpenMU.DataModel.Composition;
using MUnique.OpenMU.Interfaces;

/// <summary>
/// A boss encounter: which monster is the boss, where and when it appears, and its phases.
/// </summary>
public class BossEncounterDefinition
{
    /// <summary>
    /// Gets or sets the name of the encounter, which is shown in the messages.
    /// </summary>
    [Display(Name = "Nombre", Description = "P. ej. \"Kundun Reborn\". Se usa en los mensajes y en el comando /boss; tiene que ser único.")]
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this encounter is active.
    /// </summary>
    [Display(Name = "Activo")]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Gets or sets the number of the monster which is the boss.
    /// </summary>
    [Display(Name = "Número de monstruo del boss")]
    public short MonsterNumber { get; set; }

    /// <summary>
    /// Gets or sets the multiplier of the health of the boss.
    /// </summary>
    [Display(Name = "Multiplicador de vida", Description = "Sobre la vida del monstruo. 1 = sin cambios.")]
    [Range(0.1, 1000)]
    public double HealthMultiplier { get; set; } = 1;

    /// <summary>
    /// Gets or sets the multiplier of the damage of the boss.
    /// </summary>
    [Display(Name = "Multiplicador de daño", Description = "Sobre el daño del monstruo. 1 = sin cambios.")]
    [Range(0.1, 100)]
    public double DamageMultiplier { get; set; } = 1;

    /// <summary>
    /// Gets or sets the number of the map on which the boss appears.
    /// </summary>
    [Display(Name = "Número de mapa")]
    public short MapNumber { get; set; }

    /// <summary>
    /// Gets or sets the points at which the boss can appear. One of them is chosen randomly.
    /// </summary>
    [Display(Name = "Lugares de aparición", Description = "Se elige uno al azar en cada encuentro. Sin lugares = un punto caminable al azar del mapa.")]
    [MemberOfAggregate]
    public ICollection<BossEncounterPoint> SpawnPoints { get; set; } = new List<BossEncounterPoint>();

    /// <summary>
    /// Gets or sets the radius of the arena around the boss. The mechanics affect the players inside of it.
    /// </summary>
    [Display(Name = "Radio de la arena", Description = "Las mecánicas afectan a los jugadores que están a esta distancia del boss o menos.")]
    [Range(1, 100)]
    public byte ArenaRadius { get; set; } = 18;

    /// <summary>
    /// Gets or sets a value indicating whether the monsters of the boss which appear on the map by themselves are hidden during the encounter.
    /// </summary>
    [Display(Name = "Ocultar el spawn automático", Description = "Si el monstruo ya aparece solo en el mapa (como Kundun en Kalima 7), se oculta mientras dura el encuentro y vuelve al terminar.")]
    public bool HideAutomaticSpawns { get; set; } = true;

    /// <summary>
    /// Gets or sets the points in time at which the encounter starts.
    /// </summary>
    [Display(Name = "Horarios", Description = "Día y hora de inicio, en la zona horaria del servidor. Sin horarios = solo se inicia a mano con /boss iniciar.")]
    [MemberOfAggregate]
    public ICollection<BossEncounterScheduleEntry> Schedule { get; set; } = new List<BossEncounterScheduleEntry>();

    /// <summary>
    /// Gets or sets the minutes before the start, at which the encounter is announced to all players.
    /// </summary>
    [Display(Name = "Anuncios (minutos antes)", Description = "P. ej. 10, 5 y 1.")]
    public IList<int> AnnouncementMinutes { get; set; } = new List<int>();

    /// <summary>
    /// Gets or sets the time after the start, at which the last phase starts regardless of the health of the boss.
    /// <see cref="TimeSpan.Zero"/> means never.
    /// </summary>
    [Display(Name = "Enrage por tiempo", Description = "Pasado este tiempo, empieza la última fase aunque el boss tenga más vida. 0 = nunca.")]
    public TimeSpan SoftEnrageAfter { get; set; }

    /// <summary>
    /// Gets or sets the maximum duration. When it's reached, the boss retreats and the encounter fails.
    /// </summary>
    [Display(Name = "Duración máxima", Description = "Al cumplirse, el boss hace un último ataque y se retira: el encuentro falla.")]
    public TimeSpan MaximumDuration { get; set; } = TimeSpan.FromMinutes(20);

    /// <summary>
    /// Gets or sets the damage of the last attack of the boss before it retreats, in percent of the maximum health of the hit player.
    /// </summary>
    [Display(Name = "Daño al retirarse (%)", Description = "Daño a todos los jugadores de la arena cuando se cumple la duración máxima.")]
    [Range(0, 100)]
    public int RetreatDamagePercent { get; set; }

    /// <summary>
    /// Gets or sets the message which announces the encounter to all players.
    /// Placeholders: {0} = name, {1} = map, {2} = minutes, {3} = x, {4} = y.
    /// </summary>
    [Display(Name = "Mensaje de anuncio", Description = "{0} = nombre, {1} = mapa, {2} = minutos, {3} = x, {4} = y.")]
    public LocalizedString AnnouncementMessage { get; set; }

    /// <summary>
    /// Gets or sets the message which is shown to all players when the boss appears.
    /// Placeholders: {0} = name, {1} = map, {2} = x, {3} = y.
    /// </summary>
    [Display(Name = "Mensaje de aparición", Description = "{0} = nombre, {1} = mapa, {2} = x, {3} = y.")]
    public LocalizedString StartMessage { get; set; }

    /// <summary>
    /// Gets or sets the message which is shown to all players when the boss has been defeated.
    /// Placeholders: {0} = name, {1} = name of the character which killed the boss.
    /// </summary>
    [Display(Name = "Mensaje de victoria", Description = "{0} = nombre, {1} = personaje que lo mató.")]
    public LocalizedString DefeatedMessage { get; set; }

    /// <summary>
    /// Gets or sets the message which is shown to all players when the boss retreats.
    /// Placeholder: {0} = name.
    /// </summary>
    [Display(Name = "Mensaje de retirada", Description = "{0} = nombre.")]
    public LocalizedString RetreatMessage { get; set; }

    /// <summary>
    /// Gets or sets the phases of the encounter.
    /// </summary>
    [Display(Name = "Fases", Description = "Cada fase empieza cuando la vida del boss baja a su porcentaje. Sin fases = un boss sin mecánicas.")]
    [MemberOfAggregate]
    public ICollection<BossEncounterPhase> Phases { get; set; } = new List<BossEncounterPhase>();

    /// <inheritdoc />
    public override string ToString() => this.Name;

    /// <summary>
    /// Gets the next start of the encounter, which is at or after the specified point in time.
    /// </summary>
    /// <param name="from">The point in time, in the time zone of the server.</param>
    /// <returns>The next start, in the time zone of the server; <c>null</c>, if the encounter has no schedule.</returns>
    public DateTime? GetNextStart(DateTime from)
    {
        return this.Schedule.Count == 0 ? null : this.Schedule.Min(entry => entry.GetNextStart(from));
    }

    /// <summary>
    /// Gets the phases, ordered by their health threshold, starting with the highest.
    /// </summary>
    /// <returns>The ordered phases.</returns>
    public IReadOnlyList<BossEncounterPhase> GetOrderedPhases()
    {
        return this.Phases.OrderByDescending(phase => phase.HealthThreshold).ToList();
    }
}
