// <copyright file="CashShopTransaction.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.CashShop;

/// <summary>
/// A change of the points of an account: points which it got (e.g. for a referral) or spent in the cash shop.
/// </summary>
public class CashShopTransaction
{
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the id of the account.
    /// </summary>
    public Guid AccountId { get; set; }

    /// <summary>
    /// Gets or sets the amount of points: positive when the account got them, negative when it spent them.
    /// </summary>
    public long Amount { get; set; }

    /// <summary>
    /// Gets or sets the points of the account after the change.
    /// </summary>
    public long PointsAfter { get; set; }

    /// <summary>
    /// Gets or sets the reason, e.g. "purchase", "referral" or "gm".
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional reference which makes the change unique for the account,
    /// e.g. "referral:&lt;account id&gt;", so that the same points are never given twice.
    /// </summary>
    public string? Reference { get; set; }

    /// <summary>
    /// Gets or sets a free text, e.g. the name of the bought product.
    /// </summary>
    public string? Comment { get; set; }

    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
