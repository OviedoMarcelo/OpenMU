// <copyright file="PrestigeLevelDefinition.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.Prestige;

using System.Globalization;
using MUnique.OpenMU.DataModel.Composition;
using MUnique.OpenMU.GameLogic.PlugIns.WeeklyQuests;

/// <summary>
/// The rewards of reaching a prestige level.
/// </summary>
public class PrestigeLevelDefinition
{
    /// <summary>
    /// Gets or sets the prestige level, starting at 1.
    /// </summary>
    [Display(Name = "Prestigio", Description = "El nivel de prestigio en el que se dan estos premios, empezando por 1.")]
    [Range(1, int.MaxValue)]
    public int Level { get; set; } = 1;

    /// <summary>
    /// Gets or sets the identifier of the title which is unlocked, as configured in the Achievements plugin.
    /// </summary>
    [Display(Name = "Título", Description = "Opcional. Id de un título configurado en el plugin Achievements, p. ej. \"prestige-1\".")]
    [StringLength(64)]
    public string? TitleId { get; set; }

    /// <summary>
    /// Gets or sets the rewards. They have to fit into the inventory, otherwise the prestige is not done.
    /// </summary>
    [Display(Name = "Premios", Description = "Tienen que entrar en el inventario; si no, el prestigio no se hace y el jugador tiene que liberar espacio.")]
    [MemberOfAggregate]
    public ICollection<WeeklyQuestReward> Rewards { get; set; } = new List<WeeklyQuestReward>();

    /// <inheritdoc />
    public override string ToString() => this.Level.ToString(CultureInfo.InvariantCulture);
}
