// <copyright file="CosmeticSlot.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.Cosmetics;

/// <summary>
/// The armor slot which a cosmetic piece covers. The order is the one of the item groups
/// and of the item slots of the armor, starting with the helm.
/// </summary>
public enum CosmeticSlot
{
    /// <summary>
    /// The helm, item group 7.
    /// </summary>
    [Display(Name = "Casco")]
    Helm,

    /// <summary>
    /// The armor, item group 8.
    /// </summary>
    [Display(Name = "Armadura")]
    Armor,

    /// <summary>
    /// The pants, item group 9.
    /// </summary>
    [Display(Name = "Pantalón")]
    Pants,

    /// <summary>
    /// The gloves, item group 10.
    /// </summary>
    [Display(Name = "Guantes")]
    Gloves,

    /// <summary>
    /// The boots, item group 11.
    /// </summary>
    [Display(Name = "Botas")]
    Boots,
}
