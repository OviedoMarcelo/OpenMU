# C2 F5 10 - MerchantPrices (by server)

## Is sent when

When a player opens the store of a merchant NPC, right before the store item list, and again when that store changes while it's open.

## Causes the following actions on the client side

The client shows these prices in NPC stores instead of the ones it calculates: the buying price (times the pieces of a stack, when the price is the one of a piece), and a third of it as the selling price. It replaces the prices of a previous message.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC2  | [Packet type](PacketTypes.md) |
| 1 | 2 |    Short   |      | Packet header - length of the packet |
| 3 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 4 | 1 |    Byte   | 0x10  | Packet header - sub packet type identifier |
| 6 | 2 | ShortLittleEndian |  | PriceCount |
| 8 | MerchantPrice.Length * PriceCount | Array of MerchantPrice |  | Prices; The price of each item and level which a merchant store sells for a price of its own. |

### MerchantPrice Structure

The price which merchants ask for an item of a level.

Length: 9 Bytes

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 | Byte |  | ItemGroup |
| 1 | 1 | Byte |  | ItemLevel |
| 2 | 2 | ShortLittleEndian |  | ItemNumber |
| 4 | 4 | IntegerLittleEndian |  | Price; The buying price in zen. |
| 8 | 1 | Boolean |  | IsPricePerPiece; If set, the item stacks and the price is the one of a piece: a stack costs it times its pieces. |