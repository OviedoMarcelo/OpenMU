# C2 F5 11 - NpcQuestDialog (by server)

## Is sent when

A player talks to an NPC whose talk is the current step of a quest, and the client told with the ClientFeatures message that it shows these dialogs.

## Causes the following actions on the client side

The client shows a dialog with the portrait and name of the NPC, the text and the buttons to accept or decline. The answer is sent back with the NpcQuestDialogAnswer message, with the same dialog id.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC2  | [Packet type](PacketTypes.md) |
| 1 | 2 |    Short   |      | Packet header - length of the packet |
| 3 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 4 | 1 |    Byte   | 0x11  | Packet header - sub packet type identifier |
| 5 | 2 | ShortBigEndian |  | NpcId; The id of the NPC object. |
| 7 | 2 | ShortLittleEndian |  | NpcNumber; The number of the NPC definition. The client chooses the portrait by it. |
| 9 | 4 | IntegerLittleEndian |  | DialogId; The id of this dialog, which the answer refers to. |
| 13 | 32 | String |  | NpcName; The name of the NPC. |
| 45 | 48 | String |  | QuestName; The name of the quest. |
| 93 |  | String |  | Text; What the NPC says. Lines are separated by a line feed. |