// <copyright file="CashShopProductDefinition.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.CashShop;

using MUnique.OpenMU.DataModel.Configuration.Items;

/// <summary>
/// A product of the cash shop: an item (or the skin of an item) with its options and its price.
/// </summary>
public class CashShopProductDefinition
{
    /// <summary>
    /// The highest number of excellent options which an item can have.
    /// </summary>
    internal const int MaximumExcellentOptions = 6;

    /// <summary>
    /// Gets or sets the number of the product (1 to 9999). The bought items refer to it, so it must not change.
    /// </summary>
    [Display(Name = "Número", Description = "1 a 9999, único. No cambiarlo una vez que alguien lo compró.")]
    [Range(1, 9999)]
    public int Number { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the product is in the catalog.
    /// </summary>
    [Display(Name = "Activo")]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Gets or sets the tab of the shop in which the product is shown, e.g. a class.
    /// </summary>
    [Display(Name = "Pestaña", Description = "La pestaña de arriba de la tienda, por ejemplo \"DK\" o \"Extras\". Nombres cortos: el botón es angosto.")]
    public string Tab { get; set; } = "Extras";

    /// <summary>
    /// Gets or sets the category of the tab in which the product is shown.
    /// </summary>
    [Display(Name = "Categoría", Description = "La lista de la pestaña en la que aparece, por ejemplo \"Armas\" o \"Joyas\".")]
    public string Category { get; set; } = "Varios";

    /// <summary>
    /// Gets or sets the shown name. Empty for the name of the item.
    /// </summary>
    [Display(Name = "Nombre", Description = "Vacío = el nombre del ítem.")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    [Display(Name = "Descripción", Description = "Vacía = se arma con el nivel y las opciones.")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets what the player gets: the item, or its skin.
    /// </summary>
    [Display(Name = "Tipo", Description = "Ítem: va al inventario. Skin: el personaje desbloquea la skin del ítem (de una pieza de armadura, el set entero); tiene que existir en Skins.")]
    public CashShopProductType Type { get; set; }

    /// <summary>
    /// Gets or sets the item.
    /// </summary>
    [Display(Name = "Ítem")]
    public virtual ItemDefinition? Item { get; set; }

    /// <summary>
    /// Gets or sets the item level.
    /// </summary>
    [Display(Name = "Nivel del ítem")]
    [Range(0, 15)]
    public byte ItemLevel { get; set; }

    /// <summary>
    /// Gets or sets the amount of items (or of pieces, when the item stacks).
    /// </summary>
    [Display(Name = "Cantidad")]
    [Range(1, 255)]
    public byte Quantity { get; set; } = 1;

    /// <summary>
    /// Gets or sets a value indicating whether the item has its skill.
    /// </summary>
    [Display(Name = "Skill", Description = "Si el ítem tiene skill.")]
    public bool Skill { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the item has luck.
    /// </summary>
    [Display(Name = "Luck")]
    public bool Luck { get; set; }

    /// <summary>
    /// Gets or sets the level of the additional option.
    /// </summary>
    [Display(Name = "Nivel de opción adicional", Description = "0 a 4 (+4 a +16 en armas y sets).")]
    [Range(0, 4)]
    public byte OptionLevel { get; set; }

    /// <summary>
    /// Gets or sets the excellent options which the item always has, as a bit mask.
    /// </summary>
    [Display(Name = "Opciones excelentes fijas", Description = "Máscara de bits como en /item: 1, 2, 4, 8, 16, 32. 63 = todas.")]
    [Range(0, 63)]
    public byte ExcellentOptions { get; set; }

    /// <summary>
    /// Gets or sets the number of excellent options which the player chooses when buying, in addition to the fixed ones.
    /// The shop shows a variant per combination.
    /// </summary>
    [Display(Name = "Opciones excelentes a elección", Description = "Cuántas opciones excelentes elige el jugador al comprar (además de las fijas). 0 = ninguna. Con 2 hay hasta 15 combinaciones.")]
    [Range(0, MaximumExcellentOptions)]
    public byte ExcellentOptionChoices { get; set; }

    /// <summary>
    /// Gets or sets the price.
    /// </summary>
    [Display(Name = "Precio")]
    [Range(1, int.MaxValue)]
    public int Price { get; set; } = 100;
}
