// <copyright file="ScheduledEvent.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.PeriodicTasks;

/// <summary>
/// An event which starts by itself at fixed times of the day, e.g. Blood Castle or an invasion.
/// </summary>
/// <param name="Name">The name which is shown to the players.</param>
/// <param name="Duration">How long the event lasts after each start.</param>
/// <param name="Starts">The times of the day at which the event starts, in the time zone of the server, sorted.</param>
public sealed record ScheduledEvent(string Name, TimeSpan Duration, IReadOnlyList<TimeOnly> Starts);
