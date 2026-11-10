// <copyright file="CashShopBalance.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.CashShop;

/// <summary>
/// The points of the cash shop which an account has (e.g. "Cyber Points").
/// Every change is recorded as a <see cref="CashShopTransaction"/>, too.
/// </summary>
public class CashShopBalance
{
    /// <summary>
    /// Gets or sets the id of the account.
    /// </summary>
    public Guid AccountId { get; set; }

    /// <summary>
    /// Gets or sets the points. It's never negative.
    /// </summary>
    public long Points { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the last change.
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}
