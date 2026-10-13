// <copyright file="SeasonPassConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.SeasonPass;

using MUnique.OpenMU.DataModel.Composition;
using MUnique.OpenMU.GameLogic.PlugIns.WeeklyQuests;
using MUnique.OpenMU.Interfaces;

/// <summary>
/// The configuration of the <see cref="SeasonPassPlugIn"/>.
/// </summary>
public class SeasonPassConfiguration
{
    /// <summary>
    /// The name of the pass which is used when none is configured.
    /// </summary>
    public const string DefaultDisplayName = "Pase";

    /// <summary>
    /// The placeholder in the messages which is replaced by the <see cref="DisplayName"/>.
    /// </summary>
    public const string NamePlaceholder = "{name}";

    /// <summary>
    /// The name of the points of the pass which is used when none is configured.
    /// </summary>
    public const string DefaultPointsName = "Cyber Points";

    /// <summary>
    /// The placeholder in the messages which is replaced by the <see cref="PointsName"/>.
    /// </summary>
    public const string PointsPlaceholder = "{points}";

    /// <summary>
    /// Gets the default configuration, with an example season which is not active.
    /// </summary>
    public static SeasonPassConfiguration Default => new()
    {
        MiniGameExperience = new List<SeasonPassMiniGameExperience>
        {
            new() { MiniGameType = MiniGameType.BloodCastle, ParticipationExperience = 30, CompletionExperience = 70, RequiredSuccess = MiniGameSuccessFlags.WinnerOrInWinningParty },
            new() { MiniGameType = MiniGameType.DevilSquare, ParticipationExperience = 30, CompletionExperience = 50, RequiredSuccess = MiniGameSuccessFlags.Alive },
            new() { MiniGameType = MiniGameType.ChaosCastle, ParticipationExperience = 30, CompletionExperience = 100, RequiredSuccess = MiniGameSuccessFlags.Winner },
        },
        Seasons = new List<SeasonDefinition>
        {
            new()
            {
                Id = "s1",
                Name = "Temporada 1",
                IsActive = false,
                Start = new DateTime(2026, 11, 1),
                End = new DateTime(2027, 1, 1),
                ExperiencePerLevel = 1000,
                Levels = Enumerable.Range(1, 10).Select(level => new SeasonLevelDefinition
                {
                    Level = level,
                    FreeRewards = new List<WeeklyQuestReward>
                    {
                        new() { RewardType = WeeklyQuestRewardType.Money, Amount = 1_000_000 * level },
                    },
                    PremiumRewards = new List<WeeklyQuestReward>
                    {
                        new() { RewardType = WeeklyQuestRewardType.Experience, Amount = 2_000_000 * level },
                    },
                }).ToList(),
            },
        },
    };

    /// <summary>
    /// Gets or sets the name of the pass which is shown to the players, in the game and on the website.
    /// </summary>
    [Display(Name = "Nombre del pase", Description = "Cómo se llama el pase para los jugadores, en el juego y en la web (p. ej. \"Pase\"). Hasta 32 caracteres se ven en el juego. En los mensajes se escribe {name}.")]
    public LocalizedString DisplayName { get; set; } = DefaultDisplayName;

    /// <summary>
    /// Gets or sets the name of the points (the experience) of the pass which is shown to the players, in the game and on the website.
    /// </summary>
    [Display(Name = "Nombre de los puntos", Description = "Cómo se llaman los puntos con los que se sube de nivel en el pase (p. ej. \"Cyber Points\"). Hasta 32 caracteres se ven en el juego. En los mensajes se escribe {points}.")]
    public LocalizedString PointsName { get; set; } = DefaultPointsName;

    /// <summary>
    /// Gets or sets the experience of the pass for a completed daily quest, unless the quest sets its own.
    /// </summary>
    [Display(Name = "Puntos por quest diaria", Description = "Puntos del pase por cada quest diaria completada, salvo que la quest tenga sus propios \"Puntos del pase\".")]
    [Range(0, int.MaxValue)]
    public int DailyQuestExperience { get; set; } = 100;

    /// <summary>
    /// Gets or sets the experience of the pass for a completed weekly quest, unless the quest sets its own.
    /// </summary>
    [Display(Name = "Puntos por quest semanal", Description = "Puntos del pase por cada quest semanal completada (también el bonus por completarlas todas), salvo que la quest tenga sus propios \"Puntos del pase\".")]
    [Range(0, int.MaxValue)]
    public int WeeklyQuestExperience { get; set; } = 300;

    /// <summary>
    /// Gets or sets the experience of the pass for a completed quest which is done once (e.g. a story chapter), unless the quest sets its own.
    /// </summary>
    [Display(Name = "Puntos por quest de una vez", Description = "Puntos del pase por cada quest de una sola vez (p. ej. un capítulo de la historia), salvo que la quest tenga sus propios \"Puntos del pase\".")]
    [Range(0, int.MaxValue)]
    public int OnceQuestExperience { get; set; } = 200;

    /// <summary>
    /// Gets or sets the number of killed monsters which give <see cref="MonsterKillsExperience"/>. 0 means that kills give nothing.
    /// </summary>
    [Display(Name = "Monstruos por puntos", Description = "Cada cuántos monstruos matados se ganan puntos del pase jugando. 0 = matar monstruos no da puntos del pase.")]
    [Range(0, int.MaxValue)]
    public int MonsterKillsPerExperience { get; set; } = 200;

    /// <summary>
    /// Gets or sets the experience of the pass for every <see cref="MonsterKillsPerExperience"/> killed monsters.
    /// </summary>
    [Display(Name = "Puntos por monstruos", Description = "Puntos del pase que se ganan cada vez que se llega a \"Monstruos por puntos\".")]
    [Range(0, int.MaxValue)]
    public int MonsterKillsExperience { get; set; } = 10;

    /// <summary>
    /// Gets or sets a value indicating whether the actions while the MU Helper is running count, too.
    /// Offline leveling never counts.
    /// </summary>
    [Display(Name = "Cuenta el MU Helper", Description = "Si está activado, lo que se hace con el MU Helper encendido suma puntos del pase. El offlevel nunca suma.")]
    public bool CountMuHelperActivity { get; set; }

    /// <summary>
    /// Gets or sets the experience of the pass for each killed monster of a specific kind, e.g. bosses and monsters of invasions.
    /// </summary>
    [Display(Name = "Bonus por monstruo", Description = "Puntos del pase por matar monstruos específicos, p. ej. jefes o monstruos de invasiones (Golden, Red Dragon, White Wizard). Se suman a los puntos por monstruos y cuenta para el tope semanal.")]
    [MemberOfAggregate]
    [ScaffoldColumn(true)]
    public ICollection<SeasonPassMonsterBonus> MonsterBonuses { get; set; } = new List<SeasonPassMonsterBonus>();

    /// <summary>
    /// Gets or sets the experience of the pass for each week of the season which can be gained from sources with a weekly limit.
    /// The limit of the weeks which an account didn't use is kept. 0 means no limit.
    /// </summary>
    [Display(Name = "Tope semanal", Description = "Puntos del pase que se pueden ganar por semana matando monstruos (incluye los bonus por monstruo). Lo que no se usa se acumula para las semanas siguientes. Las quests y los eventos no tienen tope. 0 = sin tope.")]
    [Range(0, int.MaxValue)]
    public int WeeklyCappedExperience { get; set; } = 500;

    /// <summary>
    /// Gets or sets the experience of the pass at the end of mini games, e.g. Blood Castle.
    /// </summary>
    [Display(Name = "Puntos por eventos", Description = "Puntos del pase al terminar un evento: por participar (seguir adentro hasta el final) y adicional por completarlo.")]
    [MemberOfAggregate]
    [ScaffoldColumn(true)]
    public ICollection<SeasonPassMiniGameExperience> MiniGameExperience { get; set; } = new List<SeasonPassMiniGameExperience>();

    /// <summary>
    /// Gets or sets a value indicating whether the premium track is available.
    /// </summary>
    [Display(Name = "Premium habilitado", Description = "Si está desactivado, solo existe el pase gratis: no se reclaman ni se muestran premios premium.")]
    public bool PremiumEnabled { get; set; }

    /// <summary>
    /// Gets or sets the message which is shown when the pass reached a new level.
    /// Placeholder: {0} = level.
    /// </summary>
    [Display(Name = "Mensaje de nivel nuevo", Description = "{0} = nivel del pase.")]
    public LocalizedString LevelUpMessage { get; set; } = "[{name}] ¡Llegaste al nivel {0}! Usá /pase reclamar para recibir tus premios.";

    /// <summary>
    /// Gets or sets the message which is shown when the rewards of a level have been handed out.
    /// Placeholders: {0} = level, {1} = rewards.
    /// </summary>
    [Display(Name = "Mensaje de premio recibido", Description = "{0} = nivel del pase, {1} = premios.")]
    public LocalizedString ClaimedMessage { get; set; } = "[{name}] Nivel {0}: recibiste {1}.";

    /// <summary>
    /// Gets or sets the message which is shown when the rewards of a level couldn't be handed out.
    /// Placeholder: {0} = level.
    /// </summary>
    [Display(Name = "Mensaje de premio pendiente", Description = "Se muestra cuando no hay lugar en el inventario o se supera el máximo de zen. {0} = nivel del pase.")]
    public LocalizedString RewardPendingMessage { get; set; } = "[{name}] Liberá espacio en el inventario para recibir el premio del nivel {0}.";

    /// <summary>
    /// Gets or sets the message which is shown when the weekly limit has been reached.
    /// </summary>
    [Display(Name = "Mensaje de tope semanal", Description = "Se muestra una vez al llegar al tope semanal de puntos por monstruos.")]
    public LocalizedString WeeklyCapReachedMessage { get; set; } = "[{name}] Llegaste al tope semanal de {points} por monstruos. Las quests y los eventos siguen sumando.";

    /// <summary>
    /// Gets or sets the message which is shown when entering the game while there are rewards to claim.
    /// Placeholder: {0} = season name.
    /// </summary>
    [Display(Name = "Mensaje de premios para reclamar", Description = "Se muestra al entrar al juego si hay premios sin reclamar. {0} = nombre de la temporada.")]
    public LocalizedString RewardsAvailableMessage { get; set; } = "[{name}] Tenés premios del {0} para reclamar. Usá /pase reclamar.";

    /// <summary>
    /// Gets or sets the message which is shown when the premium pass has been activated.
    /// Placeholder: {0} = season name.
    /// </summary>
    [Display(Name = "Mensaje de premium activado", Description = "{0} = nombre de la temporada.")]
    public LocalizedString PremiumActivatedMessage { get; set; } = "[{name}] ¡Se activó tu pase premium de {0}! Usá /pase reclamar para recibir los premios premium.";

    /// <summary>
    /// Gets or sets the seasons.
    /// </summary>
    [Display(Name = "Temporadas", Description = "Las temporadas del pase. Cuenta la primera activa cuyo período incluye la fecha actual. El progreso se guarda por el Id de la temporada.")]
    [MemberOfAggregate]
    [ScaffoldColumn(true)]
    public ICollection<SeasonDefinition> Seasons { get; set; } = new List<SeasonDefinition>();

    /// <summary>
    /// Gets the season which is running at the specified point in time.
    /// </summary>
    /// <param name="serverTime">The point in time, in the time zone of the server.</param>
    /// <returns>The running season, if any.</returns>
    public SeasonDefinition? GetRunningSeason(DateTime serverTime)
    {
        return this.Seasons.FirstOrDefault(s => s.IsRunning(serverTime));
    }
}
