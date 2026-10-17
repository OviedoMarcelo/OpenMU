// <copyright file="EventScheduleViewPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView;

using System.Runtime.InteropServices;
using System.Text;
using MUnique.OpenMU.GameLogic.PlugIns.PeriodicTasks;
using MUnique.OpenMU.GameLogic.Views;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using MUnique.OpenMU.Network.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IEventScheduleViewPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.EventScheduleViewPlugIn_Name), Description = nameof(PlugInResources.EventScheduleViewPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("D9A43F6B-2C81-4E57-B0F2-6A8E15C3D742")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public class EventScheduleViewPlugIn : IEventScheduleViewPlugIn
{
    /// <summary>
    /// The size of the name field of the message, in bytes.
    /// </summary>
    private const int NameLength = 32;

    /// <summary>
    /// The size of the time zone name field of the message, in bytes.
    /// </summary>
    private const int TimeZoneNameLength = 24;

    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="EventScheduleViewPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public EventScheduleViewPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask ShowEventScheduleAsync(IReadOnlyList<ScheduledEvent> events, TimeOnly now, string timeZoneName)
    {
        if (this._player.Connection is not { Connected: true } connection)
        {
            return;
        }

        var secondOfDay = (uint)now.ToTimeSpan().TotalSeconds;

        // The client can't count more than a byte.
        var entries = events.Take(byte.MaxValue).ToList();
        if (entries.Count == 0)
        {
            // An empty list is sent as one message without an event, so that the client forgets the previous ones.
            await SendEntryAsync(connection, null, 0, 0, secondOfDay, timeZoneName).ConfigureAwait(false);
            return;
        }

        for (var i = 0; i < entries.Count; i++)
        {
            await SendEntryAsync(connection, entries[i], (byte)i, (byte)entries.Count, secondOfDay, timeZoneName).ConfigureAwait(false);
        }
    }

    private static ValueTask SendEntryAsync(IConnection connection, ScheduledEvent? entry, byte index, byte count, uint secondOfDay, string timeZoneName)
    {
        // An event can't start more often than every minute, so there are at most 1440 starts; a byte limits them to 255.
        var starts = entry?.Starts.Take(byte.MaxValue).ToList() ?? [];

        int Write()
        {
            var size = EventScheduleEntryRef.GetRequiredSize(starts.Count);
            var span = connection.Output.GetSpan(size)[..size];
            var packet = new EventScheduleEntryRef(span)
            {
                Index = index,
                Count = count,
                StartCount = (byte)starts.Count,
                DurationMinutes = (ushort)Math.Clamp(entry?.Duration.TotalMinutes ?? 0, 0, ushort.MaxValue),
                ServerSecondOfDay = secondOfDay,
                Name = Fit(entry?.Name, NameLength),
                TimeZoneName = Fit(timeZoneName, TimeZoneNameLength),
            };

            for (var i = 0; i < starts.Count; i++)
            {
                var target = packet[i];
                target.MinuteOfDay = (ushort)((starts[i].Hour * 60) + starts[i].Minute);
            }

            return size;
        }

        return connection.SendAsync(Write);
    }

    /// <summary>
    /// Cuts the text, so that it fits into a string field of the specified size, without cutting a character in half.
    /// </summary>
    private static string Fit(string? text, int maxBytes)
    {
        text ??= string.Empty;
        if (Encoding.UTF8.GetByteCount(text) <= maxBytes)
        {
            return text;
        }

        var length = Math.Min(text.Length, maxBytes);
        while (length > 0 && Encoding.UTF8.GetByteCount(text.AsSpan(0, length)) > maxBytes)
        {
            length--;
        }

        return text[..length];
    }
}
