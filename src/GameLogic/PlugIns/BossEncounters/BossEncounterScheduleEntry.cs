// <copyright file="BossEncounterScheduleEntry.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.BossEncounters;

using System.Globalization;
using MUnique.OpenMU.DataModel.Composition;

/// <summary>
/// A point in time of the week at which a boss encounter starts.
/// </summary>
[HorizontalLayout]
public class BossEncounterScheduleEntry
{
    private static readonly TimeSpan MinimumRepeatInterval = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the day on which the encounter starts.
    /// </summary>
    [Display(Name = "Día", Order = 1)]
    public BossEncounterDay Day { get; set; }

    /// <summary>
    /// Gets or sets the time of the day, in the time zone of the server.
    /// </summary>
    [Display(Name = "Hora", Description = "En la zona horaria del servidor.", Order = 2)]
    public TimeOnly Time { get; set; }

    /// <summary>
    /// Gets or sets the interval in which the encounter starts again on the same day, after <see cref="Time"/>.
    /// </summary>
    [Display(Name = "Repetir cada", Description = "P. ej. hora 12:00 y cada 4 h = 12, 16 y 20 h. Se repite hasta el fin del día. 0 = una vez.", Order = 3)]
    public TimeSpan RepeatEvery { get; set; }

    /// <summary>
    /// Gets or sets the maximum random delay which is added to each start, so that players can't know the exact time.
    /// </summary>
    [Display(Name = "Retraso al azar hasta", Description = "Se suma al inicio un tiempo al azar de hasta este valor. Los anuncios ya avisan la hora real. 0 = exacto.", Order = 4)]
    public TimeSpan RandomDelayUpTo { get; set; }

    /// <summary>
    /// Gets the next start of this entry, which is at or after the specified point in time, without the random delay.
    /// </summary>
    /// <param name="from">The point in time, in the time zone of the server.</param>
    /// <returns>The next start, in the time zone of the server.</returns>
    public DateTime GetNextStart(DateTime from)
    {
        for (var days = 0; days <= 7; days++)
        {
            var date = from.Date.AddDays(days);
            if (!this.IsOnDay(date.DayOfWeek))
            {
                continue;
            }

            foreach (var start in this.GetStartsOfDay(date))
            {
                if (start >= from)
                {
                    return start;
                }
            }
        }

        // Not reachable, because every day of the week is checked.
        return DateTime.MaxValue;
    }

    /// <summary>
    /// Gets a random delay of up to <see cref="RandomDelayUpTo"/>.
    /// </summary>
    /// <returns>The delay.</returns>
    public TimeSpan GetRandomDelay()
    {
        var maximumSeconds = (int)Math.Min(int.MaxValue - 1, this.RandomDelayUpTo.TotalSeconds);
        return maximumSeconds <= 0 ? TimeSpan.Zero : TimeSpan.FromSeconds(Rand.NextInt(0, maximumSeconds + 1));
    }

    /// <inheritdoc />
    public override string ToString()
    {
        var text = $"{this.Day} {this.Time.ToString("HH:mm", CultureInfo.InvariantCulture)}";
        return this.RepeatEvery > TimeSpan.Zero
            ? text + " cada " + this.RepeatEvery.ToString(@"hh\:mm", CultureInfo.InvariantCulture)
            : text;
    }

    private bool IsOnDay(DayOfWeek dayOfWeek)
    {
        return this.Day == BossEncounterDay.EveryDay || (int)this.Day == (int)dayOfWeek + 1;
    }

    private IEnumerable<DateTime> GetStartsOfDay(DateTime date)
    {
        var start = date + this.Time.ToTimeSpan();
        if (this.RepeatEvery <= TimeSpan.Zero)
        {
            yield return start;
            yield break;
        }

        // A tiny interval would make a start every few seconds.
        var interval = this.RepeatEvery < MinimumRepeatInterval ? MinimumRepeatInterval : this.RepeatEvery;
        var endOfDay = date.AddDays(1);
        for (; start < endOfDay; start += interval)
        {
            yield return start;
        }
    }
}
