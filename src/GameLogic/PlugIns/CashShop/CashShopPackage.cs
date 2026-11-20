// <copyright file="CashShopPackage.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.CashShop;

/// <summary>
/// A package (product) of the cash shop, shown with the 3D model of its item.
/// </summary>
/// <param name="Seq">The number of the package, unique in the whole catalog.</param>
/// <param name="CategorySeq">The category in which it's shown.</param>
/// <param name="Name">The name.</param>
/// <param name="Description">The description.</param>
/// <param name="Variants">The variants. A package with one variant is bought directly.</param>
public sealed record CashShopPackage(int Seq, int CategorySeq, string Name, string Description, IReadOnlyList<CashShopVariant> Variants)
{
    /// <summary>
    /// Gets the item code which the client uses to show the item: group * 512 + number.
    /// </summary>
    public ushort ItemCode => (ushort)((this.Variants[0].Item.Definition.Group * 512) + this.Variants[0].Item.Definition.Number);

    /// <summary>
    /// Gets the number of the product, which is the same as the one of the package.
    /// </summary>
    public int ProductSeq => this.Seq;
}
