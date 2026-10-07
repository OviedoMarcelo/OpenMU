# C2 F5 07 - PlayerTitle (by server)

## Is sent when

When a player with a title came into view, when a player changed its title, and after the client requested the list of available chat commands (for the titles of the players in view).

## Causes the following actions on the client side

The client shows the title below the name of the player, or removes it when the text is empty.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC2  | [Packet type](PacketTypes.md) |
| 1 | 2 |    Short   |   44   | Packet header - length of the packet |
| 3 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 4 | 1 |    Byte   | 0x07  | Packet header - sub packet type identifier |
| 6 | 2 | ShortBigEndian |  | PlayerId; The id of the player, as in the AddCharactersToScope message. |
| 8 | 4 | IntegerLittleEndian |  | Color; The color of the text, as 32 bit ARGB value. |
| 12 | 32 | String |  | Text; The title. An empty text removes the title of the player. |