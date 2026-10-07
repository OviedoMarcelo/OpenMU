// <copyright file="SeasonDefinition.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.SeasonPass;

using MUnique.OpenMU.DataModel.Composition;

/// <summary>
/// A season of the season pass.
/// </summary>
public class SeasonDefinition
{
    /// <summary>
    /// The highest level a season can have. More wouldn't fit into the window of the client.
    /// </summary>
    public const int MaximumLevel = 100;

    /// <summary>
    /// Gets or sets the identifier of the season. The progress of the accounts is stored by it.
    /// </summary>
    [Display(Name = "Id", Description = "Identificador único y estable, p. ej. \"s1\". El progreso se guarda con este valor: una temporada nueva necesita un Id nuevo.")]
    [Required]
    [StringLength(64)]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name which is shown to the player.
    /// </summary>
    [Display(Name = "Nombre", Description = "P. ej. \"Temporada 1\". Hasta 32 caracteres se ven en el juego.")]
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this season is active.
    /// </summary>
    [Display(Name = "Activa")]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Gets or sets the start of the season, in the time zone of the server.
    /// </summary>
    [Display(Name = "Inicio", Description = "Fecha y hora de inicio, en la zona horaria del servidor.")]
    public DateTime Start { get; set; }

    /// <summary>
    /// Gets or sets the end of the season, in the time zone of the server.
    /// </summary>
    [Display(Name = "Fin", Description = "Fecha y hora de fin, en la zona horaria del servidor. Después ya no se gana XP ni se reclaman premios.")]
    public DateTime End { get; set; }

    /// <summary>
    /// Gets or sets the experience of the pass which is needed for each level.
    /// </summary>
    [Display(Name = "XP por nivel")]
    [Range(1, int.MaxValue)]
    public int ExperiencePerLevel { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the levels with their rewards.
    /// </summary>
    [Display(Name = "Niveles", Description = "Los niveles con sus premios gratis y premium. Un nivel sin premios también cuenta para el máximo.")]
    [MemberOfAggregate]
    [ScaffoldColumn(true)]
    public ICollection<SeasonLevelDefinition> Levels { get; set; } = new List<SeasonLevelDefinition>();

    /// <inheritdoc />
    public override string ToString() => this.Name;

    /// <summary>
    /// Gets the highest level of the pass.
    /// </summary>
    /// <returns>The highest configured level.</returns>
    public int GetMaximumLevel()
    {
        return this.Levels.Count == 0 ? 0 : Math.Min(MaximumLevel, this.Levels.Max(l => l.Level));
    }

    /// <summary>
    /// Gets the level which is reached with the specified experience.
    /// </summary>
    /// <param name="experience">The experience of the pass.</param>
    /// <returns>The level, up to the maximum level.</returns>
    public int GetLevel(long experience)
    {
        return (int)Math.Min(this.GetMaximumLevel(), experience / Math.Max(1, this.ExperiencePerLevel));
    }

    /// <summary>
    /// Determines whether the season is running at the specified point in time.
    /// </summary>
    /// <param name="serverTime">The point in time, in the time zone of the server.</param>
    /// <returns><c>true</c>, if the season is active and running.</returns>
    public bool IsRunning(DateTime serverTime)
    {
        return this.IsActive && !string.IsNullOrWhiteSpace(this.Id) && this.Start <= serverTime && serverTime < this.End;
    }
}
