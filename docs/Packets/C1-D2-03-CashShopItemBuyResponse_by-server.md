# C1 D2 03 - CashShopItemBuyResponse (by server)

## Is sent when

The player bought an item in the cash shop.

## Causes the following actions on the client side

The client shows the result (0 = success, 1 = not enough points, 2 = storage full, 4 = not available, 6 = can't be bought, 255 = error) and, on success, asks for the points and the storage again.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |   9   | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xD2  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x03  | Packet header - sub packet type identifier |
| 4 | 1 | Byte |  | Result |
| 5 | 4 | IntegerLittleEndian |  | LeftCount |