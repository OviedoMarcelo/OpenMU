// <copyright file="CashShopCatalogFiles.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.CashShop;

using System.Text;

/// <summary>
/// The catalog files which the client of the cash shop reads ("IBSCategory.txt", "IBSPackage.txt", "IBSProduct.txt"),
/// and their version. The client keeps the files in a folder per version and downloads them when the server
/// announces a version it doesn't have yet.
/// </summary>
/// <remarks>
/// The fields of a line are separated by '@'. The client decodes the files with the ANSI code page of Windows,
/// so they're encoded as Windows-1252 (see <see cref="Encode"/>).
/// </remarks>
public sealed class CashShopCatalogFiles
{
    /// <summary>
    /// The file name of the categories.
    /// </summary>
    public const string CategoryFileName = "IBSCategory.txt";

    /// <summary>
    /// The file name of the packages.
    /// </summary>
    public const string PackageFileName = "IBSPackage.txt";

    /// <summary>
    /// The file name of the products.
    /// </summary>
    public const string ProductFileName = "IBSProduct.txt";

    private const string LineEnd = "\r\n";

    /// <summary>
    /// The value of the gift flag which enables the gift button. Gifts are not supported, so it's never used.
    /// </summary>
    private const int NoGift = 0;

    private static readonly Encoding Windows1252 = CreateEncoding();

    private CashShopCatalogFiles(string categories, string packages, string products, int salesZone)
    {
        this.Categories = categories;
        this.Packages = packages;
        this.Products = products;

        var hash = Fnv1A(categories + "\n" + packages + "\n" + products);
        this.SalesZone = (ushort)salesZone;
        this.Year = (ushort)((hash / 1000) % 10000);
        this.YearId = (ushort)(hash % 1000);
    }

    /// <summary>
    /// Gets the content of <see cref="CategoryFileName"/>.
    /// </summary>
    public string Categories { get; }

    /// <summary>
    /// Gets the content of <see cref="PackageFileName"/>.
    /// </summary>
    public string Packages { get; }

    /// <summary>
    /// Gets the content of <see cref="ProductFileName"/>.
    /// </summary>
    public string Products { get; }

    /// <summary>
    /// Gets the sales zone, the first part of the version.
    /// </summary>
    public ushort SalesZone { get; }

    /// <summary>
    /// Gets the "year", the second part of the version. It's derived from the content.
    /// </summary>
    public ushort Year { get; }

    /// <summary>
    /// Gets the "year id", the third part of the version. It's derived from the content.
    /// </summary>
    public ushort YearId { get; }

    /// <summary>
    /// Gets the version like the client names its folder, e.g. "777.0123.456".
    /// </summary>
    public string Version => $"{this.SalesZone:000}.{this.Year:0000}.{this.YearId:000}";

    /// <summary>
    /// Creates the files of a catalog.
    /// </summary>
    /// <param name="catalog">The catalog.</param>
    /// <param name="salesZone">The sales zone of the version.</param>
    /// <returns>The files.</returns>
    public static CashShopCatalogFiles Create(CashShopCatalog catalog, int salesZone)
    {
        var categories = new StringBuilder();
        var order = 0;
        foreach (var category in catalog.Categories)
        {
            // ProductDisplaySeq @ Name @ EventFlag @ OpenFlag @ ParentProductDisplaySeq @ DisplayOrder @ Root
            Append(categories, category.Seq, Clean(category.Name), 0, 1, category.ParentSeq ?? 0, ++order, category.ParentSeq is null ? 1 : 0);
        }

        var packages = new StringBuilder();
        var products = new StringBuilder();
        var currency = Clean(catalog.CurrencyName);
        foreach (var package in catalog.Packages)
        {
            var itemCode = package.ItemCode.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var priceSeqs = string.Join("|", package.Variants.Select(v => v.PriceSeq));

            // ProductDisplaySeq @ ViewOrder @ PackageProductSeq @ PackageProductName @ PackageProductType @ Price
            // @ Description @ Caution @ SalesFlag @ GiftFlag @ StartDate @ EndDate @ CapsuleFlag @ CapsuleCount
            // @ ProductCashName @ PricUnitName @ DeleteFlag @ EventFlag @ ProductAmount @ ProductSeqList
            // @ InGamePackageID @ ProductCashSeq @ PriceCount @ PriceSeqList @ DeductMileageFlag @ CashType @ CashTypeFlag
            Append(
                packages,
                package.CategorySeq,
                package.Seq,
                package.Seq,
                Clean(package.Name),
                0,
                package.Variants.Min(v => v.Price),
                Clean(package.Description),
                string.Empty,
                1,
                NoGift,
                "200001010000",
                "209912312359",
                0,
                0,
                currency,
                currency,
                0,
                0,
                1,
                package.ProductSeq,
                itemCode,
                0,
                package.Variants.Count,
                priceSeqs,
                0,
                0,
                0);

            foreach (var variant in package.Variants)
            {
                // ProductSeq @ ProductName @ PropertyName @ Value @ UnitName @ Price @ PriceSeq @ PropertyType @ MustFlag
                // @ vOrder @ DeleteFlag @ StorageGroup @ ShareFlag @ InGamePackageID @ PropertySeq @ ProductType @ UnitType
                Append(products, package.ProductSeq, Clean(variant.Name), string.Empty, string.Empty, string.Empty, variant.Price, variant.PriceSeq, 0, 0, 0, 0, 0, 0, itemCode, 0, 0, 0);
                if (variant.Item.Quantity > 1)
                {
                    // A property row with the amount, which the client shows as quantity (property 11).
                    Append(products, package.ProductSeq, Clean(variant.Name), "Cantidad", variant.Item.Quantity, string.Empty, variant.Price, variant.PriceSeq, 0, 0, 1, 0, 0, 0, itemCode, 11, 0, 0);
                }
            }
        }

        return new CashShopCatalogFiles(categories.ToString(), packages.ToString(), products.ToString(), salesZone);
    }

    /// <summary>
    /// Encodes a file like the client expects it: Windows-1252, without byte order mark.
    /// </summary>
    /// <param name="content">The content.</param>
    /// <returns>The bytes.</returns>
    public static byte[] Encode(string content) => Windows1252.GetBytes(content);

    /// <summary>
    /// Gets the content of a file by its name.
    /// </summary>
    /// <param name="fileName">The file name, e.g. "IBSPackage.txt" (case doesn't matter).</param>
    /// <returns>The content; <c>null</c>, if there is no such file.</returns>
    public string? GetFile(string fileName)
    {
        if (string.Equals(fileName, CategoryFileName, StringComparison.OrdinalIgnoreCase))
        {
            return this.Categories;
        }

        if (string.Equals(fileName, PackageFileName, StringComparison.OrdinalIgnoreCase))
        {
            return this.Packages;
        }

        if (string.Equals(fileName, ProductFileName, StringComparison.OrdinalIgnoreCase))
        {
            return this.Products;
        }

        return null;
    }

    /// <summary>
    /// Removes what would break the format of a line: separators, line breaks and characters which Windows-1252 doesn't have.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The cleaned text.</returns>
    internal static string Clean(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            builder.Append(c switch
            {
                '@' or '|' => ' ',
                '\r' or '\n' or '\t' => ' ',
                _ when c < 0x20 => ' ',
                _ => c,
            });
        }

        // Characters which Windows-1252 doesn't have become '?' when the file is encoded.
        return builder.ToString().Trim();
    }

    private static void Append(StringBuilder builder, params object[] fields)
    {
        builder.Append(string.Join('@', fields.Select(f => Convert.ToString(f, System.Globalization.CultureInfo.InvariantCulture))));
        builder.Append(LineEnd);
    }

    private static uint Fnv1A(string text)
    {
        var hash = 2166136261;
        foreach (var b in Encoding.UTF8.GetBytes(text))
        {
            hash ^= b;
            hash *= 16777619;
        }

        return hash;
    }

    private static Encoding CreateEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252, EncoderFallback.ReplacementFallback, DecoderFallback.ReplacementFallback);
    }
}
