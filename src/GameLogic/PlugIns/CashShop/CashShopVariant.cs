// <copyright file="CashShopVariant.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.CashShop;

/// <summary>
/// A variant of a package, which the player picks when buying, e.g. a combination of excellent options.
/// </summary>
/// <param name="PriceSeq">The number of the variant (price), unique in the whole catalog.</param>
/// <param name="Name">The name.</param>
/// <param name="Price">The price.</param>
/// <param name="Item">The item.</param>
public sealed record CashShopVariant(int PriceSeq, string Name, int Price, CashShopItemSpec Item);
