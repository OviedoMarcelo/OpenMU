# C2 F5 08 - SeasonPassState (by server)

## Is sent when

After the client requested the list of available chat commands, and when the season pass of the account changed: experience, claimed rewards or the premium track.

## Causes the following actions on the client side

The client shows the season pass in the quests window. Without a running season, the name is empty and there are no levels.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC2  | [Packet type](PacketTypes.md) |
| 1 | 2 |    Short   |      | Packet header - length of the packet |
| 3 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 4 | 1 |    Byte   | 0x08  | Packet header - sub packet type identifier |
| 5 | 1 | Boolean |  | IsPremium; The premium track is active for the account. |
| 6 | 2 | ShortLittleEndian |  | Level; The reached level of the pass. |
| 8 | 2 | ShortLittleEndian |  | MaximumLevel |
| 10 | 2 | ShortLittleEndian |  | LevelCount |
| 12 | 4 | IntegerLittleEndian |  | ExperienceInLevel; The experience towards the next level. |
| 16 | 4 | IntegerLittleEndian |  | ExperiencePerLevel |
| 20 | 4 | IntegerLittleEndian |  | SecondsUntilEnd; The seconds until the season ends. |
| 24 | 32 | String |  | SeasonName; The name of the season; empty, if no season is running. |
| 56 | SeasonPassLevel.Length * LevelCount | Array of SeasonPassLevel |  | Levels; The levels with their rewards, ordered by level. |

### SeasonPassLevel Structure

A level of the season pass with its rewards.

Length: 132 Bytes

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 2 | ShortLittleEndian |  | Level |
| 2 | 1 | Boolean |  | IsFreeClaimed; The rewards of the free track have been handed out. |
| 3 | 1 | Boolean |  | IsPremiumClaimed; The rewards of the premium track have been handed out. |
| 4 | 64 | String |  | FreeRewards; The rewards of the free track as text, in the language of the player; empty, if there are none. |
| 68 | 64 | String |  | PremiumRewards; The rewards of the premium track as text, in the language of the player; empty, if there are none. |