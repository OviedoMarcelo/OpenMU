// <copyright file="AchievementsConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.Achievements;

using MUnique.OpenMU.DataModel.Composition;
using MUnique.OpenMU.GameLogic.PlugIns.WeeklyQuests;
using MUnique.OpenMU.Interfaces;

/// <summary>
/// The configuration of the <see cref="AchievementsPlugIn"/>.
/// </summary>
public class AchievementsConfiguration
{
    /// <summary>
    /// Gets the default configuration, with a few example achievements and titles.
    /// </summary>
    public static AchievementsConfiguration Default => new()
    {
        Titles = new List<TitleDefinition>
        {
            new() { Id = "exterminator", Text = "Exterminador", Color = "#7CFC00" },
            new() { Id = "bc-slayer", Text = "Blood Castle Slayer", Color = "#FF4040" },
            new() { Id = "lord-arena", Text = "Lord of Arena", Color = "#FFD700" },
            new() { Id = "chaos-master", Text = "Chaos Master", Color = "#C080FF" },
            new() { Id = "legend", Text = "Leyenda", Color = "#00E5FF" },

            // Unlocked by the prestige levels of the Prestige plugin.
            new() { Id = "prestige-1", Text = "Prestigio I", Color = "#E0E0E0" },
            new() { Id = "prestige-2", Text = "Prestigio II", Color = "#FFB347" },
            new() { Id = "prestige-3", Text = "Prestigio III", Color = "#FF6EC7" },
        },
        Achievements = new List<AchievementDefinition>
        {
            new()
            {
                Id = "kill-10000",
                Name = "Exterminador",
                Description = "Matá 10.000 monstruos.",
                ObjectiveType = AchievementObjectiveType.KillMonsters,
                RequiredCount = 10_000,
                TitleId = "exterminator",
                Rewards = new List<WeeklyQuestReward>
                {
                    new() { RewardType = WeeklyQuestRewardType.Money, Amount = 10_000_000 },
                },
            },
            new()
            {
                Id = "bc-100",
                Name = "Blood Castle Slayer",
                Description = "Completá 100 Blood Castle.",
                ObjectiveType = AchievementObjectiveType.CompleteMiniGames,
                MiniGameType = MiniGameType.BloodCastle,
                RequiredCount = 100,
                TitleId = "bc-slayer",
            },
            new()
            {
                Id = "item-15",
                Name = "Herrero legendario",
                Description = "Conseguí un item +15.",
                ObjectiveType = AchievementObjectiveType.ObtainItemLevel,
                MinimumItemLevel = 15,
            },
            new()
            {
                Id = "duel-100",
                Name = "Lord of Arena",
                Description = "Ganá 100 duelos.",
                ObjectiveType = AchievementObjectiveType.WinDuels,
                RequiredCount = 100,
                TitleId = "lord-arena",
            },
            new()
            {
                Id = "chaos-500",
                Name = "Chaos Master",
                Description = "Hacé 500 combinaciones exitosas en el Chaos Machine.",
                ObjectiveType = AchievementObjectiveType.SuccessfulCraftings,
                RequiredCount = 500,
                TitleId = "chaos-master",
            },
            new()
            {
                Id = "level-400",
                Name = "Leyenda",
                Description = "Llegá a nivel 400.",
                IsHidden = true,
                ObjectiveType = AchievementObjectiveType.ReachLevel,
                RequiredCount = 400,
                TitleId = "legend",
            },
        },
    };

    /// <summary>
    /// Gets or sets a value indicating whether the monster kills count for the party members nearby, like the experience.
    /// </summary>
    [Display(Name = "Kills compartidos en party", Description = "Los kills de monstruos cuentan para los miembros de la party que estén cerca, igual que la experiencia.")]
    public bool ShareKillsWithParty { get; set; }

    /// <summary>
    /// Gets or sets the maximum of the summed experience bonus of the completed achievements, in percent.
    /// </summary>
    /// <remarks>
    /// 0 means no limit, which is also what configurations read which were created before this setting existed.
    /// </remarks>
    [Display(Name = "Bonus de XP máximo (%)", Description = "Tope de la suma de los \"Bonus de XP\" de los logros completados de un personaje y su cuenta. 0 = sin tope.")]
    [Range(0, 1000)]
    public double MaximumExperienceBonusPercent { get; set; } = 10;

    /// <summary>
    /// Gets or sets the message which is shown when an achievement has been completed and rewarded.
    /// Placeholder: {0} = achievement name.
    /// </summary>
    [Display(Name = "Mensaje de logro completado", Description = "{0} = nombre del logro.")]
    public LocalizedString CompletedMessage { get; set; } = "¡Logro desbloqueado: {0}!";

    /// <summary>
    /// Gets or sets the message which is shown when the rewards of a completed achievement couldn't be handed out.
    /// Placeholder: {0} = achievement name.
    /// </summary>
    [Display(Name = "Mensaje de premio pendiente", Description = "Se muestra cuando no hay lugar en el inventario o se supera el máximo de zen. {0} = nombre del logro.")]
    public LocalizedString RewardPendingMessage { get; set; } = "[Logro] Liberá espacio en el inventario para recibir el premio de {0}.";

    /// <summary>
    /// Gets or sets the message which is shown when a title has been unlocked.
    /// Placeholders: {0} = title text, {1} = title id.
    /// </summary>
    [Display(Name = "Mensaje de título desbloqueado", Description = "{0} = texto del título, {1} = id del título.")]
    public LocalizedString TitleUnlockedMessage { get; set; } = "Nuevo título: {0}. Escribí /titulo {1} para mostrarlo.";

    /// <summary>
    /// Gets or sets the titles.
    /// </summary>
    [Display(Name = "Títulos", Description = "Los títulos que se pueden mostrar debajo del nombre. Se guardan por el Id, así que no lo cambies en un título que ya tiene alguien.")]
    [MemberOfAggregate]
    [ScaffoldColumn(true)]
    public ICollection<TitleDefinition> Titles { get; set; } = new List<TitleDefinition>();

    /// <summary>
    /// Gets or sets the achievements.
    /// </summary>
    [Display(Name = "Logros", Description = "Todos los logros. El progreso se guarda por el Id, así que no lo cambies en un logro que ya está en curso.")]
    [MemberOfAggregate]
    [ScaffoldColumn(true)]
    public ICollection<AchievementDefinition> Achievements { get; set; } = new List<AchievementDefinition>();
}
