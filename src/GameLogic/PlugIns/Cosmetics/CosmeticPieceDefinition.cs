// <copyright file="CosmeticPieceDefinition.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.Cosmetics;

using System.Globalization;
using MUnique.OpenMU.DataModel.Composition;

/// <summary>
/// A cosmetic piece, which a character can wear over the item of an armor slot without changing its stats,
/// e.g. the Dragon Helm over the real helm.
/// </summary>
[HorizontalLayout]
public class CosmeticPieceDefinition
{
    /// <summary>
    /// The maximum length of the name, which the client can show.
    /// </summary>
    public const int MaximumNameLength = 32;

    /// <summary>
    /// Gets or sets the number of the piece. The unlocked and worn pieces are stored by it.
    /// </summary>
    [Display(Name = "Número", Description = "Identificador único y estable de la pieza, de 1 en adelante. Las piezas desbloqueadas se guardan con este valor, y es lo que se escribe en /cosmetico.", Order = 1)]
    [Range(1, short.MaxValue)]
    public short Number { get; set; }

    /// <summary>
    /// Gets or sets the slot which the piece covers.
    /// </summary>
    [Display(Name = "Slot", Order = 2)]
    public CosmeticSlot Slot { get; set; }

    /// <summary>
    /// Gets or sets the number of the item in the group of the slot, e.g. 1 for the Dragon Helm.
    /// </summary>
    [Display(Name = "Número de item", Description = "El número del item en el grupo del slot (casco 7, armadura 8, pantalón 9, guantes 10, botas 11), p. ej. 1 = Dragon, 16 = Black Dragon.", Order = 3)]
    [Range(0, 511)]
    public short ItemNumber { get; set; }

    /// <summary>
    /// Gets or sets the level of the item, which decides how much it glows.
    /// </summary>
    [Display(Name = "Nivel", Description = "Nivel con el que se ve la pieza (0 a 15). Desde +7 empieza a brillar.", Order = 4)]
    [Range(0, 15)]
    public byte Level { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the item glows like an excellent item.
    /// </summary>
    [Display(Name = "Brillo excelente", Order = 5)]
    public bool IsExcellent { get; set; }

    /// <summary>
    /// Gets or sets the name which is shown in the game. When it's empty, the name of the item is shown.
    /// </summary>
    [Display(Name = "Nombre", Description = "Opcional, hasta 32 caracteres. Vacío = el nombre del item.", Order = 6)]
    [StringLength(MaximumNameLength)]
    public string? Name { get; set; }

    /// <inheritdoc />
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{this.Number}: {this.Name ?? this.Slot.ToString()} ({this.ItemNumber})");
}
