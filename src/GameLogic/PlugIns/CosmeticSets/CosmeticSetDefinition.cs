// <copyright file="CosmeticSetDefinition.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.CosmeticSets;

using System.Globalization;

/// <summary>
/// A cosmetic set, which a character can wear over its armor without changing its stats.
/// </summary>
/// <remarks>
/// An armor set consists of the items with the same number in the groups of helms, armors, pants, gloves and boots,
/// so the set is defined by this number. When a group has no item with it, e.g. the helm of a set of the Magic Gladiator,
/// the real item of that slot stays visible.
/// </remarks>
public class CosmeticSetDefinition
{
    /// <summary>
    /// The maximum length of the name, which the client can show.
    /// </summary>
    public const int MaximumNameLength = 32;

    /// <summary>
    /// Gets or sets the number of the set. The unlocked and active sets are stored by it.
    /// </summary>
    [Display(Name = "Número", Description = "Identificador único y estable del set, de 1 en adelante. Los sets desbloqueados se guardan con este valor, y es lo que se escribe en /cosmetico.", Order = 1)]
    [Range(1, short.MaxValue)]
    public short Number { get; set; }

    /// <summary>
    /// Gets or sets the name which is shown in the game.
    /// </summary>
    [Display(Name = "Nombre", Description = "Lo que se ve en la ventana de sets cosméticos, hasta 32 caracteres.", Order = 2)]
    [Required]
    [StringLength(MaximumNameLength)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of the items of the armor set, e.g. 1 for the Dragon set.
    /// </summary>
    [Display(Name = "Número de item", Description = "El número que comparten el casco, la armadura, el pantalón, los guantes y las botas del set, p. ej. 1 = Dragon, 16 = Black Dragon.", Order = 3)]
    [Range(0, 511)]
    public short ItemNumber { get; set; }

    /// <summary>
    /// Gets or sets the level of the items, which decides how much they glow.
    /// </summary>
    [Display(Name = "Nivel", Description = "Nivel con el que se ven las piezas (0 a 15). Desde +7 empiezan a brillar.", Order = 4)]
    [Range(0, 15)]
    public byte Level { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the items glow like excellent items.
    /// </summary>
    [Display(Name = "Brillo excelente", Description = "Las piezas se ven como excelentes.", Order = 5)]
    public bool IsExcellent { get; set; }

    /// <inheritdoc />
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{this.Number}: {this.Name}");
}
