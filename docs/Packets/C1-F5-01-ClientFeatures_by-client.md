# C1 F5 01 - ClientFeatures (by client)

## Is sent when

A client which supports some optional features of this server tells which ones, right before the ChatCommandListRequest after the character entered the game world.

## Causes the following actions on the server side

The server remembers the features for this player and uses them, e.g. it sends NpcQuestDialog messages instead of showing the text of the NPC above its head.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |   5   | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x01  | Packet header - sub packet type identifier |
| 4 | 1 | Byte |  | Features; The flags of the supported features. 1 = NpcQuestDialog. |