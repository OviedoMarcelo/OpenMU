// <copyright file="CashShopCategory.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.CashShop;

/// <summary>
/// A tab of the cash shop: a zone (top level, <see cref="ParentSeq"/> is <c>null</c>) or a category of a zone.
/// </summary>
/// <param name="Seq">The number of the zone or category.</param>
/// <param name="Name">The name.</param>
/// <param name="ParentSeq">The zone of a category; <c>null</c> for a zone.</param>
public sealed record CashShopCategory(int Seq, string Name, int? ParentSeq);
