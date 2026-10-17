# C2 F5 0E - CosmeticSets (by server)

## Is sent when

After the client requested the list of available chat commands, after entering the game, and when the cosmetic sets of the character changed: a set was unlocked or removed, or the worn set changed.

## Causes the following actions on the client side

The client shows the sets in the cosmetic sets window, and shows the pieces of the worn set instead of the armor of the own character. The other players see the set through the usual appearance data.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC2  | [Packet type](PacketTypes.md) |
| 1 | 2 |    Short   |      | Packet header - length of the packet |
| 3 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 4 | 1 |    Byte   | 0x0E  | Packet header - sub packet type identifier |
| 5 | 1 | Byte |  | SetCount |
| 6 | 2 | ShortLittleEndian |  | ActiveSetNumber; The number of the set which the character wears; 0, if it wears none. |
| 8 | CosmeticSet.Length * SetCount | Array of CosmeticSet |  | Sets; The configured sets, ordered by number. |

### CosmeticSet Structure

A cosmetic set which a character can wear over its armor.

Length: 40 Bytes

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 2 | ShortLittleEndian |  | Number; The number of the set, which is used to wear it. |
| 2 | 2 | ShortLittleEndian |  | ItemNumber; The number of the pieces in the item groups of helms (7), armors (8), pants (9), gloves (10) and boots (11). |
| 4 | 1 | Byte |  | Level; The level which the pieces show, 0 to 15. |
| 5 | 1 | Boolean |  | IsExcellent; The pieces glow like excellent items. |
| 6 | 1 | Boolean |  | IsUnlocked; The character unlocked the set. |
| 7 | 1 | Boolean |  | CanWear; The class of the character can wear the set. |
| 8 | 32 | String |  | Name |