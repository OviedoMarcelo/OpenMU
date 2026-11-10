# C1 D2 0D - CashShopStorageItem (by server)

## Is sent when

The client asked for a page of the storage of the cash shop, after CashShopStorageItemCount, once per item of the page.

## Causes the following actions on the client side

The client adds the item to the shown storage. It gets name and model from the catalog by ProductSeq and PriceSeq.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |   33   | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xD2  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x0D  | Packet header - sub packet type identifier |
| 4 | 4 | IntegerLittleEndian |  | StorageIndex |
| 8 | 4 | IntegerLittleEndian |  | ItemSeq |
| 12 | 4 | IntegerLittleEndian |  | StorageGroupCode |
| 16 | 4 | IntegerLittleEndian |  | ProductSeq |
| 20 | 4 | IntegerLittleEndian |  | PriceSeq |
| 24 | 8 | Double |  | CashPoint |
| 32 | 1 | Byte |  | ItemType |