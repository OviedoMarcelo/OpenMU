// <copyright file="EventScheduleConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.PeriodicTasks;

/// <summary>
/// The configuration of how the timetables of the events are shown to the players.
/// </summary>
public class EventScheduleConfiguration
{
    /// <summary>
    /// Gets or sets the time zone in which the players see the times, e.g. "America/Argentina/Buenos_Aires".
    /// The timetables are still configured in the time zone of the server. Empty for the time zone of the server.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.EventScheduleConfiguration_DisplayTimeZoneId_Name), Description = nameof(PlugInResources.EventScheduleConfiguration_DisplayTimeZoneId_Description))]
    public string? DisplayTimeZoneId { get; set; } = "America/Argentina/Buenos_Aires";

    /// <summary>
    /// Gets or sets the name of the time zone which is shown to the players, e.g. "Argentina".
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.EventScheduleConfiguration_DisplayTimeZoneName_Name), Description = nameof(PlugInResources.EventScheduleConfiguration_DisplayTimeZoneName_Description))]
    public string? DisplayTimeZoneName { get; set; } = "Argentina";
}
