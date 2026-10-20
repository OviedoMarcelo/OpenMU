// <copyright file="IMerchantPricesViewPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Views.NPC;

using MUnique.OpenMU.GameLogic.NPC;

/// <summary>
/// Interface of a view whose client shows the prices which the merchant stores set
/// (see <see cref="MerchantStorePrices"/>), instead of the ones it calculates.
/// </summary>
public interface IMerchantPricesViewPlugIn : IViewPlugIn
{
    /// <summary>
    /// Sends the prices of the merchant stores, with the ones of the opened merchant first.
    /// </summary>
    ValueTask ShowMerchantPricesAsync();
}
