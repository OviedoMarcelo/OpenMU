// <copyright file="BossEncounterDrop.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.BossEncounters;

using System.Globalization;
using MUnique.OpenMU.DataModel.Composition;
using MUnique.OpenMU.DataModel.Configuration.Items;

/// <summary>
/// An item which the boss of an encounter may drop.
/// </summary>
[HorizontalLayout]
public class BossEncounterDrop
{
    /// <summary>
    /// Gets or sets the item.
    /// </summary>
    [Display(Name = "Item", Order = 1)]
    public virtual ItemDefinition? Item { get; set; }

    /// <summary>
    /// Gets or sets the chance in percent, that the item drops.
    /// </summary>
    [Display(Name = "Chance (%)", Description = "Se tira una vez por cada unidad de \"Cantidad\".", Order = 2)]
    [Range(0, 100)]
    public double Chance { get; set; } = 100;

    /// <summary>
    /// Gets or sets the number of items which may drop. The chance applies to each of them.
    /// </summary>
    [Display(Name = "Cantidad", Order = 3)]
    [Range(1, 50)]
    public int Quantity { get; set; } = 1;

    /// <summary>
    /// Gets or sets the level of the item.
    /// </summary>
    [Display(Name = "Nivel del item", Order = 4)]
    [Range(0, 15)]
    public byte ItemLevel { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the item has its skill.
    /// </summary>
    [Display(Name = "Skill", Order = 5)]
    public bool Skill { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the item has luck.
    /// </summary>
    [Display(Name = "Luck", Order = 6)]
    public bool Luck { get; set; }

    /// <summary>
    /// Gets or sets the level of the additional option.
    /// </summary>
    [Display(Name = "Nivel de opción adicional", Description = "0 a 4.", Order = 7)]
    [Range(0, 4)]
    public byte OptionLevel { get; set; }

    /// <summary>
    /// Gets or sets the excellent options as bit mask, like the /item command.
    /// </summary>
    [Display(Name = "Opciones excelentes", Description = "Máscara de bits como en /item: 1, 2, 4, 8, 16, 32. 63 = todas.", Order = 8)]
    [Range(0, 63)]
    public byte ExcellentOptions { get; set; }

    /// <inheritdoc />
    public override string ToString()
    {
        var name = this.Item?.Name.ToString() ?? "-";
        return string.Create(CultureInfo.InvariantCulture, $"{this.Quantity} x {name} +{this.ItemLevel} ({this.Chance} %)");
    }
}
