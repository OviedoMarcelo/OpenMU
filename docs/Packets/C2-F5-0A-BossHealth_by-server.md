# C2 F5 0A - BossHealth (by server)

## Is sent when

When a player is in the arena of a boss encounter: when it enters the arena, and when the health or the phase of the boss changes. When it leaves the arena or the encounter ends, it's sent with the state 0.

## Causes the following actions on the client side

The client shows a bar with the name, the phase and the health of the boss, or hides it with the state 0.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC2  | [Packet type](PacketTypes.md) |
| 1 | 2 |    Short   |   76   | Packet header - length of the packet |
| 3 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 4 | 1 |    Byte   | 0x0A  | Packet header - sub packet type identifier |
| 5 | 1 | Byte |  | State; 0 = hide the bar; 1 = show it. |
| 6 | 2 | ShortBigEndian |  | ObjectId; The id of the boss, as in the AddNpcsToScope message. |
| 8 | 1 | Byte |  | HealthPercent; The health of the boss in percent of its maximum health, 0 to 100. |
| 12 | 32 | String |  | BossName; The name of the boss. |
| 44 | 32 | String |  | PhaseName; The name of the current phase; empty without phases. |