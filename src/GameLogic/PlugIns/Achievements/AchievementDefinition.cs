// <copyright file="AchievementDefinition.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.Achievements;

using System.Globalization;
using MUnique.OpenMU.DataModel.Composition;
using MUnique.OpenMU.GameLogic.PlugIns.WeeklyQuests;

/// <summary>
/// The definition of an achievement, e.g. "Kill 10.000 monsters".
/// </summary>
public class AchievementDefinition
{
    /// <summary>
    /// Gets or sets the identifier of the achievement. The progress is stored by it.
    /// </summary>
    [Display(Name = "Id", Description = "Identificador único y estable, p. ej. \"kill-10000\". El progreso se guarda con este valor, así que no lo cambies en un logro que ya está en curso.")]
    [Required]
    [StringLength(64)]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name which is shown to the player.
    /// </summary>
    [Display(Name = "Nombre")]
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description which is shown to the player.
    /// </summary>
    [Display(Name = "Descripción")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this achievement is active.
    /// An inactive achievement makes no progress, but the completed ones are still listed.
    /// </summary>
    [Display(Name = "Activo")]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether this achievement is hidden until it's completed.
    /// </summary>
    [Display(Name = "Oculto", Description = "No aparece en /logros ni en la web hasta que se completa.")]
    public bool IsHidden { get; set; }

    /// <summary>
    /// Gets or sets whose progress the achievement counts.
    /// </summary>
    [Display(Name = "Alcance", Description = "Personaje: cada personaje tiene su progreso. Cuenta: suman todos los personajes de la cuenta y el título queda para todos.")]
    public AchievementScope Scope { get; set; }

    /// <summary>
    /// Gets or sets the type of the objective.
    /// </summary>
    [Display(Name = "Tipo de objetivo")]
    public AchievementObjectiveType ObjectiveType { get; set; }

    /// <summary>
    /// Gets or sets the required count to complete the achievement.
    /// For <see cref="AchievementObjectiveType.ReachLevel"/>, <see cref="AchievementObjectiveType.ReachMasterLevel"/>
    /// and <see cref="AchievementObjectiveType.ReachResets"/>, it's the level or number of resets to reach.
    /// </summary>
    [Display(Name = "Cantidad requerida", Description = "Para \"Llegar a nivel\", \"Llegar a nivel master\" y \"Llegar a resets\" es el nivel o la cantidad de resets.")]
    [Range(1, int.MaxValue)]
    public int RequiredCount { get; set; } = 1;

    /// <summary>
    /// Gets or sets the monster which has to be killed, for <see cref="AchievementObjectiveType.KillMonsters"/>.
    /// If it's empty, every monster counts.
    /// </summary>
    [Display(Name = "Monstruo", Description = "Solo para \"Matar monstruos\". Vacío = cualquier monstruo.")]
    public virtual MonsterDefinition? Monster { get; set; }

    /// <summary>
    /// Gets or sets the map on which the objective has to be done. If it's empty, every map counts.
    /// </summary>
    [Display(Name = "Mapa", Description = "Opcional, para \"Matar monstruos\" y \"Matar jugadores\". Vacío = cualquier mapa.")]
    public virtual GameMapDefinition? Map { get; set; }

    /// <summary>
    /// Gets or sets the type of the mini game, for <see cref="AchievementObjectiveType.CompleteMiniGames"/>.
    /// <see cref="DataModel.Configuration.MiniGameType.Undefined"/> means any mini game.
    /// </summary>
    [Display(Name = "Evento", Description = "Solo para \"Completar eventos\". Undefined = cualquier evento.")]
    public MiniGameType MiniGameType { get; set; }

    /// <summary>
    /// Gets or sets the minimum level of the item, for <see cref="AchievementObjectiveType.ObtainItemLevel"/>.
    /// </summary>
    [Display(Name = "Nivel del item", Description = "Solo para \"Conseguir un item +N\": el nivel mínimo, p. ej. 15. Cuenta al subirlo con jewels o con el Chaos Machine, o al juntarlo del piso.")]
    [Range(0, 15)]
    public int MinimumItemLevel { get; set; }

    /// <summary>
    /// Gets or sets the minimum level of a killed player, for <see cref="AchievementObjectiveType.KillPlayers"/>.
    /// </summary>
    [Display(Name = "Nivel mínimo de la víctima", Description = "Solo para \"Matar jugadores\". Evita farmear con personajes bajos. Las víctimas conectadas desde la misma IP nunca cuentan.")]
    [Range(0, int.MaxValue)]
    public int MinimumVictimLevel { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the title which is unlocked by completing the achievement.
    /// </summary>
    [Display(Name = "Título", Description = "Opcional. Id del título que se desbloquea al completarlo (ver la lista de títulos).")]
    [StringLength(64)]
    public string? TitleId { get; set; }

    /// <summary>
    /// Gets or sets the rewards.
    /// </summary>
    [Display(Name = "Premios")]
    [MemberOfAggregate]
    public ICollection<WeeklyQuestReward> Rewards { get; set; } = new List<WeeklyQuestReward>();

    /// <summary>
    /// Determines whether the progress is the current value of the character
    /// (e.g. its level), instead of a count of events.
    /// </summary>
    /// <returns><c>true</c>, if the progress is a current value.</returns>
    public bool IsAbsolute() => this.ObjectiveType is AchievementObjectiveType.ReachLevel
        or AchievementObjectiveType.ReachMasterLevel
        or AchievementObjectiveType.ReachResets
        or AchievementObjectiveType.ObtainItemLevel;

    /// <summary>
    /// Gets the count which has to be reached.
    /// </summary>
    /// <returns>The count.</returns>
    /// <remarks>
    /// An item of a level is obtained once, so its count is always 1.
    /// </remarks>
    public int GetRequiredCount() => this.ObjectiveType == AchievementObjectiveType.ObtainItemLevel ? 1 : Math.Max(1, this.RequiredCount);

    /// <inheritdoc />
    public override string ToString() => this.Name;

    /// <summary>
    /// Gets the rewards as text, e.g. for the website.
    /// </summary>
    /// <param name="culture">The culture.</param>
    /// <returns>The rewards, separated by comma.</returns>
    public string GetRewardsText(CultureInfo culture)
    {
        return string.Join(", ", this.Rewards.Select(r => r.GetDisplayText(culture)));
    }

    /// <summary>
    /// Determines whether the objective has to be done on the map with the specified number.
    /// </summary>
    /// <param name="mapNumber">The number of the map.</param>
    /// <returns><c>true</c>, if the objective counts on this map.</returns>
    public bool IsOnMap(short? mapNumber) => this.Map is null || this.Map.Number == mapNumber;
}
