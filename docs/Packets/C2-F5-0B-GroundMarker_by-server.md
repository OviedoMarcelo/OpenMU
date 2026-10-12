# C2 F5 0B - GroundMarker (by server)

## Is sent when

When a boss marks a place for an attack which hits after a delay (kind 0), and when the attack hits (kind 1).

## Causes the following actions on the client side

The client shows a warning circle on the ground for the duration, or the effect of the hit.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC2  | [Packet type](PacketTypes.md) |
| 1 | 2 |    Short   |   12   | Packet header - length of the packet |
| 3 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 4 | 1 |    Byte   | 0x0B  | Packet header - sub packet type identifier |
| 5 | 1 | Byte |  | Kind; 0 = warning, until the attack hits; 1 = the hit. |
| 6 | 1 | Byte |  | X; The x coordinate of the center. |
| 7 | 1 | Byte |  | Y; The y coordinate of the center. |
| 8 | 1 | Byte |  | Radius; The radius in tiles. |
| 10 | 2 | ShortLittleEndian |  | DurationMilliseconds; How long the warning is shown, in milliseconds; 0 for a hit. |