// <copyright file="BossEncounterScheduleEntry.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.BossEncounters;

using System.Globalization;

/// <summary>
/// A point in time of the week at which a boss encounter starts.
/// </summary>
public class BossEncounterScheduleEntry
{
    /// <summary>
    /// Gets or sets the day on which the encounter starts.
    /// </summary>
    [Display(Name = "Día")]
    public BossEncounterDay Day { get; set; }

    /// <summary>
    /// Gets or sets the time of the day, in the time zone of the server.
    /// </summary>
    [Display(Name = "Hora", Description = "En la zona horaria del servidor.")]
    public TimeOnly Time { get; set; }

    /// <summary>
    /// Gets the next start of this entry, which is at or after the specified point in time.
    /// </summary>
    /// <param name="from">The point in time, in the time zone of the server.</param>
    /// <returns>The next start, in the time zone of the server.</returns>
    public DateTime GetNextStart(DateTime from)
    {
        var candidate = from.Date + this.Time.ToTimeSpan();
        for (var days = 0; days <= 7; days++)
        {
            var start = candidate.AddDays(days);
            if (start >= from && this.IsOnDay(start.DayOfWeek))
            {
                return start;
            }
        }

        // Not reachable, because every day of the week is checked.
        return DateTime.MaxValue;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"{this.Day} {this.Time.ToString("HH:mm", CultureInfo.InvariantCulture)}";
    }

    private bool IsOnDay(DayOfWeek dayOfWeek)
    {
        return this.Day == BossEncounterDay.EveryDay || (int)this.Day == (int)dayOfWeek + 1;
    }
}
