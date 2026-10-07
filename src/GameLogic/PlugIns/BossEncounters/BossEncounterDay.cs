// <copyright file="BossEncounterDay.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.BossEncounters;

/// <summary>
/// The day on which a boss encounter starts.
/// </summary>
/// <remarks>
/// The values are stored in the configuration of the plugin, so they must not be renumbered.
/// </remarks>
public enum BossEncounterDay
{
    /// <summary>
    /// Every day of the week.
    /// </summary>
    [Display(Name = "Todos los días")]
    EveryDay = 0,

    /// <summary>
    /// Sunday.
    /// </summary>
    [Display(Name = "Domingo")]
    Sunday = 1,

    /// <summary>
    /// Monday.
    /// </summary>
    [Display(Name = "Lunes")]
    Monday = 2,

    /// <summary>
    /// Tuesday.
    /// </summary>
    [Display(Name = "Martes")]
    Tuesday = 3,

    /// <summary>
    /// Wednesday.
    /// </summary>
    [Display(Name = "Miércoles")]
    Wednesday = 4,

    /// <summary>
    /// Thursday.
    /// </summary>
    [Display(Name = "Jueves")]
    Thursday = 5,

    /// <summary>
    /// Friday.
    /// </summary>
    [Display(Name = "Viernes")]
    Friday = 6,

    /// <summary>
    /// Saturday.
    /// </summary>
    [Display(Name = "Sábado")]
    Saturday = 7,
}
