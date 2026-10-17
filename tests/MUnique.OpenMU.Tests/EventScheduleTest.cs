// <copyright file="EventScheduleTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.GameLogic.PlugIns.InvasionEvents;
using MUnique.OpenMU.GameLogic.PlugIns.PeriodicTasks;
using MUnique.OpenMU.GameLogic.PlugIns.WanderingMerchants;
using MUnique.OpenMU.Network.Packets.ServerToClient;

/// <summary>
/// Tests for the events which are shown to the players with their timetables.
/// </summary>
[TestFixture]
public class EventScheduleTest
{
    /// <summary>
    /// Tests that the events are named and ordered like on the website, with their times sorted and without repetitions,
    /// and that events without times and the wandering merchants, which ignore their timetable, are left out.
    /// </summary>
    [Test]
    public void EventsAreNamedOrderedAndFiltered()
    {
        var plugIns = new IPeriodicTaskPlugIn[]
        {
            new GoldenInvasionPlugIn { Configuration = new PeriodicInvasionConfiguration { TaskDuration = TimeSpan.FromMinutes(30), Timetable = [new TimeOnly(16, 0), new TimeOnly(4, 0), new TimeOnly(4, 0)] } },
            new BloodCastleStartPlugIn { Configuration = new BloodCastleStartConfiguration { TaskDuration = TimeSpan.FromMinutes(20), Timetable = [new TimeOnly(2, 0)] } },
            new DevilSquareStartPlugIn { Configuration = new DevilSquareStartConfiguration { Timetable = [] } },
            new WanderingMerchantsPlugIn { Configuration = new WanderingMerchantsConfiguration { Timetable = [new TimeOnly(1, 0)] } },
            new EventScheduleSyncPlugIn(),
        };

        var events = EventSchedule.GetEvents(plugIns);

        Assert.That(events.Select(e => e.Name), Is.EqualTo(new[] { "Blood Castle", "Invasión Dorada" }));
        Assert.That(events[0].Duration, Is.EqualTo(TimeSpan.FromMinutes(20)));
        Assert.That(events[1].Starts, Is.EqualTo(new[] { new TimeOnly(4, 0), new TimeOnly(16, 0) }));
    }

    /// <summary>
    /// Tests that the times of the server (UTC) are shown in the time of Argentina (UTC-3), sorted again
    /// because the times around midnight move to the other end of the day.
    /// </summary>
    [Test]
    public void TimesAreConvertedToTheShownTimeZone()
    {
        var argentina = TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");
        var events = new[] { new ScheduledEvent("Blood Castle", TimeSpan.FromMinutes(20), [new TimeOnly(0, 0), new TimeOnly(2, 0), new TimeOnly(18, 30)]) };

        var converted = EventSchedule.ConvertTimeZone(events, TimeZoneInfo.Utc, argentina, new DateTime(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc));

        Assert.That(converted[0].Starts, Is.EqualTo(new[] { new TimeOnly(15, 30), new TimeOnly(21, 0), new TimeOnly(23, 0) }));
        Assert.That(converted[0].Duration, Is.EqualTo(TimeSpan.FromMinutes(20)));
    }

    /// <summary>
    /// Tests that the signature changes with the timetable, so that the players get the new times.
    /// </summary>
    [Test]
    public void SignatureChangesWithTheTimetable()
    {
        var before = EventSchedule.GetSignature([new ScheduledEvent("Blood Castle", TimeSpan.FromMinutes(20), [new TimeOnly(2, 0)])]);
        var after = EventSchedule.GetSignature([new ScheduledEvent("Blood Castle", TimeSpan.FromMinutes(20), [new TimeOnly(4, 0)])]);

        Assert.That(after, Is.Not.EqualTo(before));
    }

    /// <summary>
    /// Tests that the values of an event survive the way through the packet, so that none of the fields overlaps another one.
    /// </summary>
    [Test]
    public void EventScheduleEntryKeepsItsValues()
    {
        var data = new byte[EventScheduleEntryRef.GetRequiredSize(2)];
        var written = new EventScheduleEntryRef(data)
        {
            Index = 3,
            Count = 8,
            StartCount = 2,
            DurationMinutes = 135,
            ServerSecondOfDay = 86_399,
            Name = "Invasión Dorada",
            TimeZoneName = "Argentina",
        };
        var first = written[0];
        first.MinuteOfDay = 0;
        var last = written[1];
        last.MinuteOfDay = 1439;

        var read = new EventScheduleEntryRef(data);

        Assert.That(read.Index, Is.EqualTo(3));
        Assert.That(read.Count, Is.EqualTo(8));
        Assert.That(read.StartCount, Is.EqualTo(2));
        Assert.That(read.DurationMinutes, Is.EqualTo(135));
        Assert.That(read.ServerSecondOfDay, Is.EqualTo(86_399));
        Assert.That(read.Name, Is.EqualTo("Invasión Dorada"));
        Assert.That(read.TimeZoneName, Is.EqualTo("Argentina"));
        Assert.That(read[0].MinuteOfDay, Is.EqualTo(0));
        Assert.That(read[1].MinuteOfDay, Is.EqualTo(1439));
    }
}
