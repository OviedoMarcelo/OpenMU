// <copyright file="BossEncounterDefinition.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.BossEncounters;

using MUnique.OpenMU.DataModel.Composition;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.Interfaces;

/// <summary>
/// A boss encounter: which monster is the boss, where and when it appears, and its phases.
/// </summary>
/// <remarks>
/// The <see cref="DisplayAttribute.Order"/> of the properties groups them on the admin panel:
/// general, boss, place, schedule, duration, messages and phases.
/// </remarks>
public class BossEncounterDefinition
{
    /// <summary>
    /// Gets or sets the name of the encounter, which is shown in the messages.
    /// </summary>
    [Display(Name = "Nombre", Description = "P. ej. \"Kundun Reborn\". Se usa en los mensajes y en el comando /boss; tiene que ser único.", Order = 1)]
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this encounter is active.
    /// </summary>
    [Display(Name = "Activo", Description = "Un encuentro inactivo no empieza por horario, pero se puede iniciar a mano con /boss iniciar.", Order = 2)]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Gets or sets the number of the monster which is the boss.
    /// </summary>
    [Display(Name = "Boss: monstruo", Order = 10)]
    [MonsterNumber]
    public short MonsterNumber { get; set; }

    /// <summary>
    /// Gets or sets the multiplier of the health of the boss.
    /// </summary>
    [Display(Name = "Boss: multiplicador de vida", Description = "Sobre la vida del monstruo. 1 = sin cambios.", Order = 11)]
    [Range(0.1, 1000)]
    public double HealthMultiplier { get; set; } = 1;

    /// <summary>
    /// Gets or sets the multiplier of the damage of the boss.
    /// </summary>
    [Display(Name = "Boss: multiplicador de daño", Description = "Sobre el daño del monstruo. 1 = sin cambios.", Order = 12)]
    [Range(0.1, 100)]
    public double DamageMultiplier { get; set; } = 1;

    /// <summary>
    /// Gets or sets the size of the boss in percent of the normal size of the monster.
    /// </summary>
    /// <remarks>Existing configurations read 0, which means the normal size.</remarks>
    [Display(Name = "Boss: tamaño (%)", Description = "100 = normal; 150 = una vez y media. Se ve con el cliente actualizado.", Order = 13)]
    [Range(0, 1000)]
    public int ScalePercent { get; set; } = 100;

    /// <summary>
    /// Gets or sets the glow of the boss, like the glow of an item of a high level.
    /// </summary>
    [Display(Name = "Boss: brillo", Description = "Como el brillo de los items +7 a +15, o dorado como los monstruos de la invasión dorada. Se ve con el cliente actualizado.", Order = 14)]
    public MonsterGlow Glow { get; set; }

    /// <summary>
    /// Gets or sets the name which the client shows instead of the name of the monster.
    /// </summary>
    [Display(Name = "Boss: nombre en pantalla", Description = "Vacío = el nombre del monstruo. Hasta 32 caracteres. Se ve con el cliente actualizado.", Order = 15)]
    [StringLength(MonsterAppearance.MaximumNameLength)]
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the places at which the boss can appear. One of them is chosen randomly.
    /// </summary>
    [Display(Name = "Lugares de aparición", Description = "Mapa y coordenadas. Si hay varios, se elige uno al azar en cada encuentro (pueden ser de mapas distintos).", Order = 20)]
    [MemberOfAggregate]
    [ScaffoldColumn(true)]
    public ICollection<BossEncounterPoint> SpawnPoints { get; set; } = new List<BossEncounterPoint>();

    /// <summary>
    /// Gets or sets the radius of the arena around the boss. The mechanics affect the players inside of it.
    /// </summary>
    [Display(Name = "Radio de la arena", Description = "Las mecánicas afectan a los jugadores que están a esta distancia del boss o menos.", Order = 21)]
    [Range(1, 100)]
    public byte ArenaRadius { get; set; } = 18;

    /// <summary>
    /// Gets or sets a value indicating whether the monsters of the boss which appear on the map by themselves are hidden during the encounter.
    /// </summary>
    [Display(Name = "Ocultar el spawn automático", Description = "Si el monstruo ya aparece solo en el mapa (como Kundun en Kalima 7), se oculta mientras dura el encuentro y vuelve al terminar.", Order = 22)]
    public bool HideAutomaticSpawns { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum distance of the boss to the place where it appeared. When it's further away, it's
    /// teleported back, so that it can't be pulled out of its arena. 0 = no limit.
    /// </summary>
    [Display(Name = "Distancia máxima del boss", Description = "Si el boss se aleja más que esto del lugar donde apareció, vuelve teletransportado. Así no se lo puede sacar de la arena. 0 = sin límite.", Order = 23)]
    [Range(0, 255)]
    public byte LeashRadius { get; set; } = 25;

    /// <summary>
    /// Gets or sets the time after which the encounter starts again, when there are no living players in the arena,
    /// e.g. after all of them died. The boss heals completely and goes back to its first phase. 0 = never.
    /// </summary>
    [Display(Name = "Reiniciar con la arena vacía", Description = "Si no queda ningún jugador vivo en la arena durante este tiempo (p. ej. murieron todos), el boss se cura por completo y vuelve a la primera fase. La duración máxima sigue contando. 0 = nunca.", Order = 24)]
    public TimeSpan ResetAfterEmptyArena { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the percentage of health which the boss gets for each player in the arena after the first one.
    /// </summary>
    [Display(Name = "Escalado: vida por jugador (%)", Description = "Vida extra del boss por cada jugador en la arena después del primero, sobre su vida máxima. Se recalcula mientras dura y solo sube: si los jugadores se van, no baja. El % de vida actual se mantiene. 0 = sin escalado.", Order = 25)]
    [Range(0, 1000)]
    public int ScalingHealthPercentPerPlayer { get; set; }

    /// <summary>
    /// Gets or sets the percentage of damage which the boss gets for each player in the arena after the first one.
    /// </summary>
    [Display(Name = "Escalado: daño por jugador (%)", Description = "Daño extra del boss por cada jugador en la arena después del primero. Se multiplica con los otros multiplicadores de daño. 0 = sin escalado.", Order = 26)]
    [Range(0, 1000)]
    public int ScalingDamagePercentPerPlayer { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of players which are counted for the scaling.
    /// </summary>
    [Display(Name = "Escalado: máximo de jugadores", Description = "Más jugadores que estos no hacen más fuerte al boss.", Order = 27)]
    [Range(1, 1000)]
    public int ScalingMaximumPlayers { get; set; } = 20;

    /// <summary>
    /// Gets or sets the interval in which the players in the arena are counted for the scaling.
    /// </summary>
    [Display(Name = "Escalado: intervalo", Description = "Cada cuánto se cuentan los jugadores de la arena.", Order = 28)]
    public TimeSpan ScalingInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets the points in time at which the encounter starts.
    /// </summary>
    [Display(Name = "Horarios", Description = "Día y hora de inicio, en la zona horaria del servidor. Sin horarios = solo se inicia a mano con /boss iniciar.", Order = 30)]
    [MemberOfAggregate]
    [ScaffoldColumn(true)]
    public ICollection<BossEncounterScheduleEntry> Schedule { get; set; } = new List<BossEncounterScheduleEntry>();

    /// <summary>
    /// Gets or sets the minutes before the start, at which the encounter is announced to all players.
    /// </summary>
    [Display(Name = "Anuncios (minutos antes)", Description = "P. ej. 10, 5 y 1.", Order = 31)]
    public IList<int> AnnouncementMinutes { get; set; } = new List<int>();

    /// <summary>
    /// Gets or sets the message which announces the encounter to all players.
    /// Placeholders: {0} = name, {1} = map, {2} = minutes, {3} = x, {4} = y.
    /// </summary>
    [Display(Name = "Mensaje de anuncio", Description = "{0} = nombre, {1} = mapa, {2} = minutos, {3} = x, {4} = y.", Order = 32)]
    public LocalizedString AnnouncementMessage { get; set; }

    /// <summary>
    /// Gets or sets the percentages of the health of the boss at which the players of the map get a message.
    /// </summary>
    [Display(Name = "Avisos de vida (%)", Description = "P. ej. 75, 50 y 25: un mensaje dorado al mapa cuando la vida del boss baja a cada uno. Se avisa una vez por encuentro (otra vez después de un reinicio).", Order = 33)]
    public IList<int> HealthAnnouncements { get; set; } = new List<int>();

    /// <summary>
    /// Gets or sets the message for the <see cref="HealthAnnouncements"/>.
    /// </summary>
    [Display(Name = "Mensaje de aviso de vida", Description = "{0} = nombre, {1} = porcentaje.", Order = 34)]
    public LocalizedString HealthAnnouncementMessage { get; set; }

    /// <summary>
    /// Gets or sets the time after the start, at which the last phase starts regardless of the health of the boss.
    /// <see cref="TimeSpan.Zero"/> means never.
    /// </summary>
    [Display(Name = "Enrage por tiempo", Description = "Pasado este tiempo, empieza la última fase aunque el boss tenga más vida. 0 = nunca.", Order = 40)]
    public TimeSpan SoftEnrageAfter { get; set; }

    /// <summary>
    /// Gets or sets the maximum duration. When it's reached, the boss retreats and the encounter fails.
    /// </summary>
    [Display(Name = "Duración máxima", Description = "Al cumplirse, el boss hace un último ataque y se retira: el encuentro falla.", Order = 41)]
    public TimeSpan MaximumDuration { get; set; } = TimeSpan.FromMinutes(20);

    /// <summary>
    /// Gets or sets the damage of the last attack of the boss before it retreats, in percent of the maximum health of the hit player.
    /// </summary>
    [Display(Name = "Daño al retirarse (%)", Description = "Daño a todos los jugadores de la arena cuando se cumple la duración máxima.", Order = 42)]
    [Range(0, 100)]
    public int RetreatDamagePercent { get; set; }

    /// <summary>
    /// Gets or sets the message which is shown to all players when the boss appears.
    /// Placeholders: {0} = name, {1} = map, {2} = x, {3} = y.
    /// </summary>
    [Display(Name = "Mensaje de aparición", Description = "{0} = nombre, {1} = mapa, {2} = x, {3} = y.", Order = 50)]
    public LocalizedString StartMessage { get; set; }

    /// <summary>
    /// Gets or sets the message which is shown to all players when the boss has been defeated.
    /// Placeholders: {0} = name, {1} = name of the character which killed the boss.
    /// </summary>
    [Display(Name = "Mensaje de victoria", Description = "{0} = nombre, {1} = personaje que lo mató.", Order = 51)]
    public LocalizedString DefeatedMessage { get; set; }

    /// <summary>
    /// Gets or sets the message which is shown to all players when the boss retreats.
    /// Placeholder: {0} = name.
    /// </summary>
    [Display(Name = "Mensaje de retirada", Description = "{0} = nombre.", Order = 52)]
    public LocalizedString RetreatMessage { get; set; }

    /// <summary>
    /// Gets or sets the message which is shown to the players of the map, when the encounter starts again because the arena was empty.
    /// </summary>
    [Display(Name = "Mensaje de reinicio", Description = "Cuando el boss se cura porque la arena quedó vacía. {0} = nombre.", Order = 53)]
    public LocalizedString ResetMessage { get; set; }

    /// <summary>
    /// Gets or sets the items which the boss may drop, in addition to or instead of the drops of its monster.
    /// </summary>
    [Display(Name = "Drops", Description = "Items que puede tirar el boss. Caen alrededor suyo y son de quien lo mató (o de su party), como cualquier drop.", Order = 55)]
    [MemberOfAggregate]
    [ScaffoldColumn(true)]
    public ICollection<BossEncounterDrop> Drops { get; set; } = new List<BossEncounterDrop>();

    /// <summary>
    /// Gets or sets a value indicating whether the <see cref="Drops"/> replace the drops of the monster of the boss.
    /// </summary>
    [Display(Name = "Drops: reemplazar los del monstruo", Description = "Activado = el boss solo tira los drops de esta lista y el zen de abajo. Desactivado = también tira lo que tira normalmente su monstruo.", Order = 56)]
    public bool ReplaceMonsterDrops { get; set; }

    /// <summary>
    /// Gets or sets the money which the boss drops in addition.
    /// </summary>
    [Display(Name = "Drops: zen", Description = "Zen extra que tira el boss. 0 = nada.", Order = 57)]
    [Range(0, int.MaxValue)]
    public int DropMoney { get; set; }

    /// <summary>
    /// Gets or sets the minimum percentage of the damage of all players, which a player has to do to the boss to get the
    /// <see cref="ParticipationRewards"/> and the <see cref="ParticipationMoney"/>.
    /// </summary>
    [Display(Name = "Participación: daño mínimo (%)", Description = "Porcentaje del daño total que tiene que hacer un jugador (con sus invocaciones) para recibir la recompensa por participar, si el boss muere. 0 = alcanza con haberle pegado.", Order = 58)]
    [Range(0, 100)]
    public double ParticipationMinimumDamagePercent { get; set; } = 5;

    /// <summary>
    /// Gets or sets the items which each player with enough damage gets into its inventory, when the boss was defeated.
    /// </summary>
    [Display(Name = "Participación: items", Description = "Items que recibe en el inventario cada jugador que llegó al daño mínimo, si el boss muere. Con el inventario lleno, caen a sus pies y son suyos. Vacío = sin items.", Order = 59)]
    [MemberOfAggregate]
    [ScaffoldColumn(true)]
    public ICollection<BossEncounterDrop> ParticipationRewards { get; set; } = new List<BossEncounterDrop>();

    /// <summary>
    /// Gets or sets the money which each player with enough damage gets, when the boss was defeated.
    /// </summary>
    [Display(Name = "Participación: zen", Description = "Zen que recibe cada jugador que llegó al daño mínimo. 0 = nada.", Order = 60)]
    [Range(0, int.MaxValue)]
    public int ParticipationMoney { get; set; }

    /// <summary>
    /// Gets or sets the message for a player who got the reward for the participation.
    /// </summary>
    [Display(Name = "Participación: mensaje", Description = "Para cada jugador que recibe la recompensa. {0} = nombre, {1} = su % del daño.", Order = 61)]
    public LocalizedString ParticipationMessage { get; set; }

    /// <summary>
    /// Gets or sets the number of players of the ranking of the damage, which is shown when the encounter ends.
    /// </summary>
    [Display(Name = "Ranking: jugadores", Description = "Cuántos jugadores muestra el ranking de daño al terminar el encuentro, en el chat de los jugadores del mapa. 0 = sin ranking.", Order = 62)]
    [Range(0, 20)]
    public int RankingSize { get; set; } = 5;

    /// <summary>
    /// Gets or sets the title of the ranking of the damage.
    /// </summary>
    [Display(Name = "Ranking: título", Description = "Primera línea del ranking. {0} = nombre.", Order = 63)]
    public LocalizedString RankingMessage { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the health which the players restore to the players in the arena is
    /// tracked, for the ranking of the healing and its rewards.
    /// </summary>
    [Display(Name = "Curación: tenerla en cuenta", Description = "Activado = se suma la vida que cada jugador cura a los jugadores de la arena: hay ranking de curación y premios por puesto de curación. Desactivado = solo cuenta el daño.", Order = 64)]
    public bool IsHealingTracked { get; set; }

    /// <summary>
    /// Gets or sets the title of the ranking of the healing.
    /// </summary>
    [Display(Name = "Ranking de curación: título", Description = "Primera línea del ranking de curación, que se muestra si se tiene en cuenta la curación y alguien curó en la arena. {0} = nombre.", Order = 65)]
    public LocalizedString HealingRankingMessage { get; set; }

    /// <summary>
    /// Gets or sets the rewards for the places of the rankings, given in addition to the reward for the participation.
    /// </summary>
    [Display(Name = "Premios por puesto", Description = "Premios extra para los primeros puestos del ranking de daño o de curación (si se tiene en cuenta), si el boss muere. P. ej. daño puesto 1 = un item mejor, puestos 2 a 3 = otro. Se suman a la recompensa por participar. Con el inventario lleno, caen a sus pies.", Order = 66)]
    [MemberOfAggregate]
    [ScaffoldColumn(true)]
    public ICollection<BossEncounterRankReward> RankRewards { get; set; } = new List<BossEncounterRankReward>();

    /// <summary>
    /// Gets or sets the message for a player who got a reward for its place.
    /// </summary>
    [Display(Name = "Premios por puesto: mensaje", Description = "{0} = nombre, {1} = puesto, {2} = ranking (daño o curación).", Order = 67)]
    public LocalizedString RankRewardMessage { get; set; }

    /// <summary>
    /// Gets or sets the phases of the encounter.
    /// </summary>
    [Display(Name = "Fases", Description = "Cada fase empieza cuando la vida del boss baja a su porcentaje. Sin fases = un boss sin mecánicas.", Order = 70)]
    [MemberOfAggregate]
    [ScaffoldColumn(true)]
    public ICollection<BossEncounterPhase> Phases { get; set; } = new List<BossEncounterPhase>();

    /// <inheritdoc />
    public override string ToString() => this.Name;

    /// <summary>
    /// Gets the next start of the encounter, whose scheduled time is at or after the specified point in time.
    /// The random delay of its schedule entry is added, so the result differs between calls.
    /// </summary>
    /// <param name="from">The point in time, in the time zone of the server.</param>
    /// <returns>The next start, in the time zone of the server; <c>null</c>, if the encounter has no schedule.</returns>
    public DateTime? GetNextStart(DateTime from)
    {
        if (this.Schedule.Count == 0)
        {
            return null;
        }

        var (start, entry) = this.Schedule.Select(entry => (Start: entry.GetNextStart(from), Entry: entry)).MinBy(next => next.Start);
        return start == DateTime.MaxValue ? start : start + entry.GetRandomDelay();
    }

    /// <summary>
    /// Gets the appearance of the boss, which the client shows differently than the other monsters of its kind.
    /// </summary>
    /// <returns>The appearance.</returns>
    public MonsterAppearance GetBossAppearance()
    {
        var scalePercent = this.ScalePercent > 0 ? this.ScalePercent : 100;
        return new MonsterAppearance(scalePercent, this.Glow, this.DisplayName?.Trim() ?? string.Empty);
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
