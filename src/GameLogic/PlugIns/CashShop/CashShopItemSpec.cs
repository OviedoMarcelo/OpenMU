// <copyright file="CashShopItemSpec.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.CashShop;

using MUnique.OpenMU.DataModel.Configuration.Items;

/// <summary>
/// The item which a variant of a package gives.
/// </summary>
/// <param name="Definition">The item definition.</param>
/// <param name="Level">The item level.</param>
/// <param name="Quantity">The amount of items, or the pieces of a stack.</param>
/// <param name="Skill">If set, the item has its skill.</param>
/// <param name="Luck">If set, the item has luck.</param>
/// <param name="OptionLevel">The level of the additional option.</param>
/// <param name="ExcellentOptions">The excellent options as a bit mask.</param>
/// <param name="IsSkin">If set, the player gets the skin of the item instead of the item.</param>
public sealed record CashShopItemSpec(ItemDefinition Definition, byte Level, byte Quantity, bool Skill, bool Luck, byte OptionLevel, byte ExcellentOptions, bool IsSkin = false);
