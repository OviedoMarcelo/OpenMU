# C1 D2 0B - CashShopStorageItemConsumeResponse (by server)

## Is sent when

The player used an item of the storage of the cash shop.

## Causes the following actions on the client side

The client shows the result (0 = success, 1 = not in the storage, 21 = no space in the inventory, 22 = can't be used, 255 = error) and, on success, reloads the storage.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |   5   | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xD2  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x0B  | Packet header - sub packet type identifier |
| 4 | 1 | Byte |  | Result |