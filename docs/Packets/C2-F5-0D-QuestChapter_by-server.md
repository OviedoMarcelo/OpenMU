# C2 F5 0D - QuestChapter (by server)

## Is sent when

Right after the QuestDetails message of a quest which belongs to a chapter, with the same quest id.

## Causes the following actions on the client side

The client shows the quest under the name of its chapter, e.g. in the tracker of the quests in progress. Clients which don't know this message ignore it.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC2  | [Packet type](PacketTypes.md) |
| 1 | 2 |    Short   |   117   | Packet header - length of the packet |
| 3 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 4 | 1 |    Byte   | 0x0D  | Packet header - sub packet type identifier |
| 5 | 64 | String |  | Id; The id of the quest. |
| 69 | 48 | String |  | ChapterName; The name of the chapter of the quest. |