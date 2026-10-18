# C2 F5 0E - CosmeticPieces (by server)

## Is sent when

After the client requested the list of available chat commands, after entering the game, and when the cosmetic pieces of the character changed: a piece was unlocked or removed, or put on or taken off.

## Causes the following actions on the client side

The client shows the pieces in the cosmetics window, and shows the worn pieces instead of the armor of the own character. The other players see them through the usual appearance data.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC2  | [Packet type](PacketTypes.md) |
| 1 | 2 |    Short   |      | Packet header - length of the packet |
| 3 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 4 | 1 |    Byte   | 0x0E  | Packet header - sub packet type identifier |
| 6 | 2 | ShortLittleEndian |  | PieceCount |
| 8 | CosmeticPiece.Length * PieceCount | Array of CosmeticPiece |  | Pieces; The configured pieces, ordered by slot and number. |

### CosmeticPiece Structure

A cosmetic piece which a character can wear over the item of an armor slot.

Length: 42 Bytes

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 2 | ShortLittleEndian |  | Number; The number of the piece, which is used to wear it. |
| 2 | 2 | ShortLittleEndian |  | ItemNumber; The number of the item in its group. |
| 4 | 1 | Byte |  | ItemGroup; The group of the item, which tells the slot: helms (7), armors (8), pants (9), gloves (10) or boots (11). |
| 5 | 1 | Byte |  | Level; The level which the piece shows, 0 to 15. |
| 6 | 1 | Boolean |  | IsExcellent; The piece glows like an excellent item. |
| 7 | 1 | Boolean |  | IsUnlocked; The character unlocked the piece. |
| 8 | 1 | Boolean |  | CanWear; The class of the character can wear the piece. |
| 9 | 1 | Boolean |  | IsWorn; The character wears the piece. |
| 10 | 32 | String |  | Name |