// <copyright file="VaultExtensionConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.VaultExtension;

using System.ComponentModel.DataAnnotations;
using MUnique.OpenMU.DataModel.Composition;

/// <summary>
/// Configuration of the <see cref="VaultExtensionNpcPlugIn"/>.
/// </summary>
public class VaultExtensionConfiguration
{
    /// <summary>
    /// The number of the Guardián del Baúl of MU Cyber.
    /// </summary>
    public const short DefaultNpcNumber = 708;

    /// <summary>
    /// Gets or sets the number of the NPC which extends the vault.
    /// </summary>
    [Display(Name = "NPC Number", Description = "Número del NPC que extiende el baúl. 708 = Guardián del Baúl.")]
    [Range(1, short.MaxValue)]
    public short NpcNumber { get; set; } = DefaultNpcNumber;

    /// <summary>
    /// Gets or sets the items which the player has to give to extend the vault.
    /// </summary>
    [Display(Name = "Price", Description = "Los ítems que hay que tener en el inventario para extender el baúl. El NPC se los saca al aceptar.")]
    [MemberOfAggregate]
    [ScaffoldColumn(true)]
    public ICollection<VaultExtensionPrice> Price { get; set; } = new List<VaultExtensionPrice>
    {
        new() { ItemGroup = 12, ItemNumber = 31, Amount = 1, Caption = "Paquete de 10 Jewel of Soul" },
        new() { ItemGroup = 12, ItemNumber = 136, Amount = 1, Caption = "Paquete de 10 Jewel of Life" },
        new() { ItemGroup = 12, ItemNumber = 141, Amount = 1, Caption = "Paquete de 10 Jewel of Chaos" },
    };
}
