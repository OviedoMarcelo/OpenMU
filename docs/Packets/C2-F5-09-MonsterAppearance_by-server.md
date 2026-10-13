# C2 F5 09 - MonsterAppearance (by server)

## Is sent when

When a monster which looks different than the others of its kind (e.g. the boss of an encounter) came into view, right after the AddNpcsToScope message.

## Causes the following actions on the client side

The client shows the monster with the size, the glow and the name of this message, until it leaves the view.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC2  | [Packet type](PacketTypes.md) |
| 1 | 2 |    Short   |   44   | Packet header - length of the packet |
| 3 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 4 | 1 |    Byte   | 0x09  | Packet header - sub packet type identifier |
| 5 | 1 | Byte |  | Glow; The glow, like the glow of an item of that level: 0 = none, 7, 9, 11, 13 or 15; 100 = golden, like the monsters of the golden invasion. |
| 6 | 2 | ShortBigEndian |  | ObjectId; The id of the monster, as in the AddNpcsToScope message. |
| 8 | 2 | ShortLittleEndian |  | ScalePercent; The size in percent of the normal size of the monster. |
| 12 | 32 | String |  | Name; The name which is shown instead of the name of the monster. Empty for its own name. |