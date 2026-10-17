# C2 F5 0F - EventScheduleEntry (by server)

## Is sent when

After the client requested the list of available chat commands, one message is sent for each active event which starts at fixed times of the day (e.g. Blood Castle or an invasion). When a timetable changes or an event is (de)activated, the whole list is sent again.

## Causes the following actions on the client side

The client shows the events with their next start, e.g. in a window. Clients which don't know this message ignore it.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC2  | [Packet type](PacketTypes.md) |
| 1 | 2 |    Short   |      | Packet header - length of the packet |
| 3 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 4 | 1 |    Byte   | 0x0F  | Packet header - sub packet type identifier |
| 5 | 1 | Byte |  | Index; The index of this event within the list, starting at 0. |
| 6 | 1 | Byte |  | Count; The total number of events, so that the client knows when it received all of them. An empty list is sent as one message with a count of 0, so that the client forgets the previous events. |
| 7 | 1 | Byte |  | StartCount |
| 8 | 2 | ShortLittleEndian |  | DurationMinutes; How long the event lasts after each start, in minutes. |
| 10 | 4 | IntegerLittleEndian |  | ServerSecondOfDay; The current time when the message was sent, as seconds since midnight in the time zone of the times, so that the client can tell the next start without depending on its own clock. |
| 14 | 32 | String |  | Name; The name of the event. |
| 46 | 24 | String |  | TimeZoneName; The name of the time zone of the times, e.g. 'Argentina'. It's empty when the times are in the time zone of the server. |
| 70 | EventStartTime.Length * StartCount | Array of EventStartTime |  | Starts; The times of the day at which the event starts, in the time zone of the times, sorted. |

### EventStartTime Structure

A time of the day at which an event starts.

Length: 2 Bytes

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 2 | ShortLittleEndian |  | MinuteOfDay; The minutes since midnight, in the time zone of the times. |