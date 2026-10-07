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
    /// Gets the default configuration, with an example season which is not active.
    /// </summary>
    public static SeasonPassConfiguration Default => new()
    {
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
    /// Gets or sets the experience of the pass for a completed daily quest, unless the quest sets its own.
    /// </summary>
    [Display(Name = "XP por quest diaria", Description = "XP del pase por cada quest diaria completada, salvo que la quest tenga su propia \"XP del pase\".")]
    [Range(0, int.MaxValue)]
    public int DailyQuestExperience { get; set; } = 100;

    /// <summary>
    /// Gets or sets the experience of the pass for a completed weekly quest, unless the quest sets its own.
    /// </summary>
    [Display(Name = "XP por quest semanal", Description = "XP del pase por cada quest semanal completada (también el bonus por completarlas todas), salvo que la quest tenga su propia \"XP del pase\".")]
    [Range(0, int.MaxValue)]
    public int WeeklyQuestExperience { get; set; } = 300;

    /// <summary>
    /// Gets or sets the experience of the pass for a completed quest which is done once (e.g. a story chapter), unless the quest sets its own.
    /// </summary>
    [Display(Name = "XP por quest de una vez", Description = "XP del pase por cada quest de una sola vez (p. ej. un capítulo de la historia), salvo que la quest tenga su propia \"XP del pase\".")]
    [Range(0, int.MaxValue)]
    public int OnceQuestExperience { get; set; } = 200;

    /// <summary>
    /// Gets or sets the number of killed monsters which give <see cref="MonsterKillsExperience"/>. 0 means that kills give nothing.
    /// </summary>
    [Display(Name = "Monstruos por XP", Description = "Cada cuántos monstruos matados se gana XP del pase jugando. 0 = matar monstruos no da XP del pase.")]
    [Range(0, int.MaxValue)]
    public int MonsterKillsPerExperience { get; set; } = 200;

    /// <summary>
    /// Gets or sets the experience of the pass for every <see cref="MonsterKillsPerExperience"/> killed monsters.
    /// </summary>
    [Display(Name = "XP por monstruos", Description = "XP del pase que se gana cada vez que se llega a \"Monstruos por XP\".")]
    [Range(0, int.MaxValue)]
    public int MonsterKillsExperience { get; set; } = 10;

    /// <summary>
    /// Gets or sets the message which is shown when the pass reached a new level.
    /// Placeholder: {0} = level.
    /// </summary>
    [Display(Name = "Mensaje de nivel nuevo", Description = "{0} = nivel del pase.")]
    public LocalizedString LevelUpMessage { get; set; } = "[Pase] ¡Llegaste al nivel {0}! Usá /pase reclamar para recibir tus premios.";

    /// <summary>
    /// Gets or sets the message which is shown when the rewards of a level have been handed out.
    /// Placeholders: {0} = level, {1} = rewards.
    /// </summary>
    [Display(Name = "Mensaje de premio recibido", Description = "{0} = nivel del pase, {1} = premios.")]
    public LocalizedString ClaimedMessage { get; set; } = "[Pase] Nivel {0}: recibiste {1}.";

    /// <summary>
    /// Gets or sets the message which is shown when the rewards of a level couldn't be handed out.
    /// Placeholder: {0} = level.
    /// </summary>
    [Display(Name = "Mensaje de premio pendiente", Description = "Se muestra cuando no hay lugar en el inventario o se supera el máximo de zen. {0} = nivel del pase.")]
    public LocalizedString RewardPendingMessage { get; set; } = "[Pase] Liberá espacio en el inventario para recibir el premio del nivel {0}.";

    /// <summary>
    /// Gets or sets the message which is shown when entering the game while there are rewards to claim.
    /// Placeholder: {0} = season name.
    /// </summary>
    [Display(Name = "Mensaje de premios para reclamar", Description = "Se muestra al entrar al juego si hay premios sin reclamar. {0} = nombre de la temporada.")]
    public LocalizedString RewardsAvailableMessage { get; set; } = "[Pase] Tenés premios del {0} para reclamar. Usá /pase reclamar.";

    /// <summary>
    /// Gets or sets the message which is shown when the premium pass has been activated.
    /// Placeholder: {0} = season name.
    /// </summary>
    [Display(Name = "Mensaje de premium activado", Description = "{0} = nombre de la temporada.")]
    public LocalizedString PremiumActivatedMessage { get; set; } = "[Pase] ¡Se activó tu pase premium de {0}! Usá /pase reclamar para recibir los premios premium.";

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
