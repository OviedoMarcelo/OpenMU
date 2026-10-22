# C1 F5 02 - NpcQuestDialogAnswer (by client)

## Is sent when

The player chose an answer in the dialog of an NpcQuestDialog message, or closed it.

## Causes the following actions on the server side

When the player accepted, the talk counts for the quest. Otherwise nothing happens and the player can talk to the NPC again later.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |   9   | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x02  | Packet header - sub packet type identifier |
| 4 | 4 | IntegerLittleEndian |  | DialogId; The id of the dialog, as in the NpcQuestDialog message. |
| 8 | 1 | Boolean |  | Accepted; If set, the player accepted. Otherwise the player declined or closed the dialog. |