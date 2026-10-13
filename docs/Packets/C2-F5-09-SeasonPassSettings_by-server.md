# C2 F5 09 - SeasonPassSettings (by server)

## Is sent when

Before the SeasonPassState, when the client requested the list of available chat commands.

## Causes the following actions on the client side

The client shows the configured name of the pass on its button and window, and hides the premium track when it's disabled. Clients which don't know this message keep their defaults.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC2  | [Packet type](PacketTypes.md) |
| 1 | 2 |    Short   |   38   | Packet header - length of the packet |
| 3 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 4 | 1 |    Byte   | 0x09  | Packet header - sub packet type identifier |
| 5 | 1 | Boolean |  | PremiumEnabled; The premium track is available on the server. |
| 6 | 32 | String |  | ProgramName; The name of the pass, in the language of the player; empty to use the default of the client. |