// <copyright file="EventSchedule.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.PeriodicTasks;

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using MUnique.OpenMU.GameLogic.Views;

/// <summary>
/// The events which start by themselves, as they are shown to the players.
/// </summary>
public static class EventSchedule
{
    /// <summary>
    /// The names and the order of the known events, like on the website. Other events are shown by the name of their plugin, after them.
    /// </summary>
    private static readonly IReadOnlyDictionary<Guid, (string Name, int Order)> KnownEvents = new Dictionary<Guid, (string Name, int Order)>
    {
        [new Guid("95E68C14-AD87-4B3C-AF46-45B8F1C3BC2A")] = ("Blood Castle", 1),
        [new Guid("61C61A58-211E-4D6A-9EA1-D25E0C4A47C5")] = ("Devil Square", 2),
        [new Guid("3AD96A70-ED24-4979-80B8-169E461E548F")] = ("Chaos Castle", 3),
        [new Guid("A8F3C2D1-9E74-4ECB-8963-08A3697278C4")] = ("Kanturu", 4),
        [new Guid("06D18A9E-2919-4C17-9DBC-6E4F7756495C")] = ("Invasión Dorada", 5),
        [new Guid("548A76CC-242C-441C-BC9D-6C22745A2D72")] = ("Invasión Red Dragon", 6),
        [new Guid("4B5D0F55-5B26-4447-B9C0-C272E5D0A141")] = ("Invasión White Wizard", 7),
        [new Guid("6542E452-9780-45B8-85AE-4036422E9A6E")] = ("Happy Hour", 8),
    };

    /// <summary>
    /// Gets the active events which have a timetable.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The events, in the order in which they're shown.</returns>
    public static IReadOnlyList<ScheduledEvent> GetEvents(IGameContext gameContext)
    {
        return GetEvents(gameContext.PlugInManager.GetActivePlugInsOf<IPeriodicTaskPlugIn>());
    }

    /// <summary>
    /// Gets the events of the plugins which have a timetable.
    /// </summary>
    /// <param name="plugIns">The active periodic task plugins.</param>
    /// <returns>The events, in the order in which they're shown.</returns>
    public static IReadOnlyList<ScheduledEvent> GetEvents(IEnumerable<IPeriodicTaskPlugIn> plugIns)
    {
        return plugIns
            .OfType<IScheduledEventPlugIn>()
            .Select(plugIn => (Type: plugIn.GetType(), Configuration: plugIn.ScheduleConfiguration))
            .Where(e => e.Configuration is { Timetable.Count: > 0 })
            .Select(e =>
            {
                var (name, order) = KnownEvents.TryGetValue(e.Type.GUID, out var known)
                    ? known
                    : (e.Type.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? e.Type.Name, int.MaxValue);
                var starts = e.Configuration!.Timetable.Distinct().Order().ToList();
                return (Order: order, Event: new ScheduledEvent(name, e.Configuration.TaskDuration, starts));
            })
            .OrderBy(e => e.Order)
            .ThenBy(e => e.Event.Name, StringComparer.OrdinalIgnoreCase)
            .Select(e => e.Event)
            .ToList();
    }

    /// <summary>
    /// Sends the events to the player, in the time zone in which the players see the times.
    /// </summary>
    /// <param name="player">The player.</param>
    public static async ValueTask SendAsync(Player player)
    {
        var gameContext = player.GameContext;
        var configuration = gameContext.PlugInManager.GetActivePlugInsOf<IPeriodicTaskPlugIn>().OfType<EventScheduleSyncPlugIn>().FirstOrDefault()?.Configuration
                            ?? new EventScheduleConfiguration();
        var displayTimeZone = FindTimeZone(configuration.DisplayTimeZoneId);
        var utcNow = DateTime.UtcNow;
        var events = displayTimeZone is null
            ? GetEvents(gameContext)
            : ConvertTimeZone(GetEvents(gameContext), gameContext.ServerTimeZone, displayTimeZone, utcNow);
        var now = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utcNow, displayTimeZone ?? gameContext.ServerTimeZone));
        var timeZoneName = displayTimeZone is null ? string.Empty : configuration.DisplayTimeZoneName ?? string.Empty;
        await player.InvokeViewPlugInAsync<IEventScheduleViewPlugIn>(p => p.ShowEventScheduleAsync(events, now, timeZoneName)).ConfigureAwait(false);
    }

    /// <summary>
    /// Converts the times of the events from the time zone of the server to another one, as they are today.
    /// </summary>
    /// <param name="events">The events, with the times in the time zone of the server.</param>
    /// <param name="serverTimeZone">The time zone of the server.</param>
    /// <param name="targetTimeZone">The time zone in which the players see the times.</param>
    /// <param name="utcNow">The current time, which tells the day of the conversion (for daylight saving time).</param>
    /// <returns>The events with the converted times, sorted.</returns>
    public static IReadOnlyList<ScheduledEvent> ConvertTimeZone(IReadOnlyList<ScheduledEvent> events, TimeZoneInfo serverTimeZone, TimeZoneInfo targetTimeZone, DateTime utcNow)
    {
        var serverToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utcNow, serverTimeZone));
        TimeOnly Convert(TimeOnly time)
        {
            var serverTime = serverToday.ToDateTime(time, DateTimeKind.Unspecified);

            // A time which is skipped when the clocks are put forward doesn't exist on that day; it's an hour later then.
            if (serverTimeZone.IsInvalidTime(serverTime))
            {
                serverTime = serverTime.AddHours(1);
            }

            return TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(TimeZoneInfo.ConvertTimeToUtc(serverTime, serverTimeZone), targetTimeZone));
        }

        return events.Select(e => e with { Starts = e.Starts.Select(Convert).Distinct().Order().ToList() }).ToList();
    }

    /// <summary>
    /// Gets a text which changes when anything of the events changes which the players see.
    /// </summary>
    /// <param name="events">The events.</param>
    /// <returns>The text.</returns>
    public static string GetSignature(IEnumerable<ScheduledEvent> events)
    {
        return string.Join('|', events.Select(e => $"{e.Name};{e.Duration};{string.Join(',', e.Starts)}"));
    }

    private static TimeZoneInfo? FindTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return null;
        }

        try
        {
            // FindSystemTimeZoneById accepts both IANA and Windows ids on all platforms.
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return null;
        }
    }
}
