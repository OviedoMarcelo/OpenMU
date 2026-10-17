// <copyright file="IScheduledEventPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.PeriodicTasks;

/// <summary>
/// A periodic task plugin which starts at the times of its timetable, so that the players can be told when.
/// </summary>
public interface IScheduledEventPlugIn
{
    /// <summary>
    /// Gets the configuration with the timetable, or <c>null</c> if the plugin doesn't start by its timetable.
    /// </summary>
    PeriodicTaskConfiguration? ScheduleConfiguration { get; }
}
