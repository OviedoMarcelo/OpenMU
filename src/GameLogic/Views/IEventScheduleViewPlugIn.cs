// <copyright file="IEventScheduleViewPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Views;

using MUnique.OpenMU.GameLogic.PlugIns.PeriodicTasks;

/// <summary>
/// Interface of a view whose client can show when the next events start.
/// </summary>
public interface IEventScheduleViewPlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows the events with their timetables. It replaces the events which the client knew before.
    /// </summary>
    /// <param name="events">The events.</param>
    /// <param name="now">The current time of the day in the time zone of the times.</param>
    /// <param name="timeZoneName">The name of the time zone of the times, e.g. "Argentina"; empty for the time zone of the server.</param>
    ValueTask ShowEventScheduleAsync(IReadOnlyList<ScheduledEvent> events, TimeOnly now, string timeZoneName);
}
