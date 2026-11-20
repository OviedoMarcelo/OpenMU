// <copyright file="CashShopConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.CashShop;

/// <summary>
/// The configuration of the <see cref="CashShopPlugIn"/>: the catalog and its prices.
/// </summary>
public class CashShopConfiguration
{
    /// <summary>
    /// Gets or sets the name of the currency, as the shop shows it.
    /// </summary>
    [Display(Name = "Nombre de la moneda", Description = "Cómo se llama la moneda de la tienda, por ejemplo \"Cyber Coins\".")]
    public string CurrencyName { get; set; } = "Cyber Coins";

    /// <summary>
    /// Gets or sets the maximum count of bought items which wait in the storage of an account.
    /// </summary>
    [Display(Name = "Máximo de ítems en el depósito", Description = "Cuántos ítems comprados pueden esperar en el depósito de la tienda, por cuenta. Al llegar al máximo hay que usar alguno antes de comprar otro.")]
    [Range(1, 1000)]
    public int MaximumStorageItems { get; set; } = 50;

    /// <summary>
    /// Gets or sets the count of items of a page of the storage.
    /// </summary>
    [Display(Name = "Ítems por página del depósito")]
    [Range(1, 20)]
    public int StoragePageSize { get; set; } = 7;

    /// <summary>
    /// Gets or sets the sales zone of the catalog version. The client keeps the catalog files in a folder per version.
    /// </summary>
    [Display(Name = "Zona del catálogo", Description = "Primera parte de la versión del catálogo (0 a 999). Normalmente no hace falta cambiarla: la versión cambia sola con el contenido del catálogo.")]
    [Range(0, 999)]
    public int SalesZone { get; set; } = 777;

    /// <summary>
    /// Gets or sets the products. The shop shows a tab per <see cref="CashShopProductDefinition.Tab"/>, in the order of
    /// their first product, and in each tab a list per <see cref="CashShopProductDefinition.Category"/>.
    /// </summary>
    [Display(Name = "Productos", Description = "Cada producto con un número propio entre 1 y 9999, que no hay que cambiar una vez que alguien lo compró. Las pestañas aparecen en el orden de su primer producto; el juego muestra hasta 8.")]
    public ICollection<CashShopProductDefinition> Products { get; set; } = new List<CashShopProductDefinition>();
}
