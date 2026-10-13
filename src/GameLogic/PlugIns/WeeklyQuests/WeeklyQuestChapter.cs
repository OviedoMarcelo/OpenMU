// <copyright file="WeeklyQuestChapter.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.WeeklyQuests;

using MUnique.OpenMU.DataModel.Composition;

/// <summary>
/// A chapter of quests, e.g. of the story. The quests of a chapter are available after the chapter which it requires is completed.
/// </summary>
public class WeeklyQuestChapter
{
    /// <summary>
    /// Gets or sets the identifier of the chapter, which the quests refer to with <see cref="WeeklyQuestDefinition.ChapterId"/>.
    /// </summary>
    [Display(Name = "Id", Description = "Identificador único y estable, p. ej. \"cap-1\". Cada quest elige su capítulo en \"Capítulo\".")]
    [Required]
    [StringLength(64)]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name which is shown to the player.
    /// </summary>
    [Display(Name = "Nombre", Description = "P. ej. \"Capítulo I - El despertar\".")]
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the identifier of the chapter which has to be completed before the quests of this chapter are available.
    /// </summary>
    [Display(Name = "Requiere capítulo", Description = "Opcional. El capítulo que hay que completar antes. Vacío = disponible desde el inicio.")]
    [KeyOf(nameof(WeeklyQuestsConfiguration.Chapters))]
    [StringLength(64)]
    public string? PrerequisiteChapterId { get; set; }

    /// <summary>
    /// Gets or sets how many quests of the chapter which are done only once have to be completed, so that the chapter is completed.
    /// 0 means all of them.
    /// </summary>
    [Display(Name = "Quests para completarlo", Description = "Cuántas quests de una sola vez del capítulo hay que completar para desbloquear los capítulos que lo requieren. 0 = todas. Las diarias y semanales del capítulo no cuentan.")]
    [Range(0, int.MaxValue)]
    public int QuestsToComplete { get; set; }
}
