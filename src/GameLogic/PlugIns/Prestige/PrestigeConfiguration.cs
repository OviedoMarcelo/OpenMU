// <copyright file="PrestigeConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.Prestige;

using MUnique.OpenMU.DataModel.Composition;
using MUnique.OpenMU.GameLogic.PlugIns.WeeklyQuests;
using MUnique.OpenMU.Interfaces;

/// <summary>
/// The configuration of the <see cref="PrestigePlugIn"/>.
/// </summary>
public class PrestigeConfiguration
{
    /// <summary>
    /// Gets the default configuration, with titles and rewards for the first three prestige levels.
    /// </summary>
    public static PrestigeConfiguration Default => new()
    {
        Levels = Enumerable.Range(1, 3).Select(level => new PrestigeLevelDefinition
        {
            Level = level,
            TitleId = $"prestige-{level}",
            Rewards = new List<WeeklyQuestReward>
            {
                new() { RewardType = WeeklyQuestRewardType.Money, Amount = 50_000_000 * level },
            },
        }).ToList(),
    };

    /// <summary>
    /// Gets or sets the character level which is required for a prestige.
    /// </summary>
    [Display(Name = "Nivel requerido")]
    [Range(1, int.MaxValue)]
    public int RequiredLevel { get; set; } = 400;

    /// <summary>
    /// Gets or sets the master level which is required for a prestige. 0 means the maximum master level of the game configuration.
    /// </summary>
    [Display(Name = "Nivel master requerido", Description = "0 = el nivel master máximo del servidor.")]
    [Range(0, int.MaxValue)]
    public int RequiredMasterLevel { get; set; }

    /// <summary>
    /// Gets or sets the highest prestige level. 0 means no limit.
    /// </summary>
    [Display(Name = "Prestigio máximo", Description = "0 = sin límite.")]
    [Range(0, int.MaxValue)]
    public int MaximumPrestige { get; set; }

    /// <summary>
    /// Gets or sets the prestige points which every prestige gives. They're kept for a later use, e.g. a shop.
    /// </summary>
    [Display(Name = "Puntos por prestigio", Description = "Puntos de prestigio que se suman en cada prestigio. Se guardan para canjearlos más adelante.")]
    [Range(0, int.MaxValue)]
    public int PointsPerPrestige { get; set; } = 1;

    /// <summary>
    /// Gets or sets the experience bonus in percent per prestige level.
    /// </summary>
    [Display(Name = "Bonus de XP por prestigio (%)", Description = "P. ej. 1 = +1% de experiencia por cada nivel de prestigio, hasta el máximo.")]
    [Range(0, 100)]
    public double ExperienceBonusPercentPerPrestige { get; set; } = 1;

    /// <summary>
    /// Gets or sets the maximum experience bonus of the prestige, in percent. 0 means no limit.
    /// </summary>
    [Display(Name = "Bonus de XP máximo (%)", Description = "Tope del bonus de experiencia del prestigio. 0 = sin tope.")]
    [Range(0, 1000)]
    public double MaximumExperienceBonusPercent { get; set; } = 10;

    /// <summary>
    /// Gets or sets the message which is shown to all players when a character reached a prestige level.
    /// Placeholders: {0} = character name, {1} = prestige level. Empty means no announcement.
    /// </summary>
    [Display(Name = "Anuncio global", Description = "Se muestra a todos los jugadores. {0} = personaje, {1} = nivel de prestigio. Vacío = sin anuncio.")]
    public LocalizedString AnnouncementMessage { get; set; } = "¡{0} alcanzó el Prestigio {1}!";

    /// <summary>
    /// Gets or sets the prestige levels with their rewards and titles.
    /// </summary>
    [Display(Name = "Premios por prestigio", Description = "Premios y título de cada nivel de prestigio. Un nivel que no está en la lista no da premios, pero sí puntos y bonus de XP.")]
    [MemberOfAggregate]
    [ScaffoldColumn(true)]
    public ICollection<PrestigeLevelDefinition> Levels { get; set; } = new List<PrestigeLevelDefinition>();

    /// <summary>
    /// Gets the experience bonus in percent of a prestige level.
    /// </summary>
    /// <param name="prestigeLevel">The prestige level.</param>
    /// <returns>The bonus, limited to the maximum.</returns>
    public double GetExperienceBonusPercent(int prestigeLevel)
    {
        var bonus = prestigeLevel * this.ExperienceBonusPercentPerPrestige;
        return this.MaximumExperienceBonusPercent > 0 ? Math.Min(bonus, this.MaximumExperienceBonusPercent) : bonus;
    }
}
