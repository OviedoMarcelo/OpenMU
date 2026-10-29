// <copyright file="VaultExtensionPrice.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.VaultExtension;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// An item which the player has to give to the NPC of the <see cref="VaultExtensionNpcPlugIn"/>, e.g. a pack of 10 Jewels of Soul.
/// </summary>
public class VaultExtensionPrice
{
    /// <summary>
    /// Gets or sets the group of the item, e.g. 12 for the packed jewels.
    /// </summary>
    [Display(Name = "Item Group", Description = "Grupo del ítem. Los paquetes de joyas son del grupo 12.")]
    [Range(0, 15)]
    public byte ItemGroup { get; set; }

    /// <summary>
    /// Gets or sets the number of the item in its group.
    /// </summary>
    [Display(Name = "Item Number", Description = "Número del ítem dentro del grupo. Paquetes: 30 Bless, 31 Soul, 136 Life, 137 Creation, 141 Chaos.")]
    [Range(0, short.MaxValue)]
    public short ItemNumber { get; set; }

    /// <summary>
    /// Gets or sets the level of the item. For a packed jewel, 0 is a pack of 10, 1 of 20 and 2 of 30.
    /// </summary>
    [Display(Name = "Item Level", Description = "Nivel del ítem. En los paquetes de joyas: 0 = paquete de 10, 1 = de 20, 2 = de 30.")]
    [Range(0, 15)]
    public byte ItemLevel { get; set; }

    /// <summary>
    /// Gets or sets how many of these items the player has to give.
    /// </summary>
    [Display(Name = "Amount", Description = "Cuántos de estos ítems hay que entregar.")]
    [Range(1, 100)]
    public int Amount { get; set; } = 1;

    /// <summary>
    /// Gets or sets the name of the item which the NPC tells the player, e.g. "Paquete de 10 Jewel of Soul".
    /// </summary>
    [Display(Name = "Caption", Description = "Cómo lo nombra el NPC al jugador, por ejemplo \"Paquete de 10 Jewel of Soul\".")]
    public string Caption { get; set; } = string.Empty;
}
