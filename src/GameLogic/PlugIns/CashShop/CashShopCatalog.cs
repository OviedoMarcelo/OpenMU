// <copyright file="CashShopCatalog.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.CashShop;

using System.Globalization;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic.Attributes;

/// <summary>
/// The catalog of the cash shop, built from the products of the <see cref="CashShopConfiguration"/>.
/// </summary>
/// <remarks>
/// The client shows a zone (tab) per <see cref="CashShopProductDefinition.Tab"/> and in it a category per
/// <see cref="CashShopProductDefinition.Category"/>. The numbers of packages and variants are derived from the
/// number of the product and the options, so they stay the same when the catalog is built again.
/// The bought items keep everything they need, though.
/// </remarks>
public sealed class CashShopCatalog
{
    /// <summary>
    /// The highest number of a product.
    /// </summary>
    internal const int MaximumProductNumber = 9999;

    private const int FirstCategorySeq = 100;
    private const string DefaultTab = "Extras";
    private const string DefaultCategory = "Varios";

    private readonly Dictionary<int, CashShopPackage> _packagesBySeq;

    private CashShopCatalog(IReadOnlyList<CashShopCategory> categories, IReadOnlyList<CashShopPackage> packages, string currencyName)
    {
        this.Categories = categories;
        this.Packages = packages;
        this.CurrencyName = currencyName;
        this._packagesBySeq = packages.ToDictionary(p => p.Seq);
    }

    /// <summary>
    /// Gets the zones (tabs) and categories, zones first.
    /// </summary>
    public IReadOnlyList<CashShopCategory> Categories { get; }

    /// <summary>
    /// Gets the packages.
    /// </summary>
    public IReadOnlyList<CashShopPackage> Packages { get; }

    /// <summary>
    /// Gets the name of the currency.
    /// </summary>
    public string CurrencyName { get; }

    /// <summary>
    /// Builds the catalog.
    /// </summary>
    /// <param name="configuration">The configuration of the shop.</param>
    /// <param name="items">The item definitions of the game.</param>
    /// <returns>The catalog.</returns>
    public static CashShopCatalog Build(CashShopConfiguration configuration, IEnumerable<ItemDefinition> items)
    {
        var itemList = items.ToList();
        var tabs = new List<string>();
        var categoryKeys = new List<(int TabIndex, string Name)>();
        var packages = new List<CashShopPackage>();

        var products = configuration.Products
            .Where(p => p.IsActive && p.Item is not null && p.Number is >= 1 and <= MaximumProductNumber)
            .GroupBy(p => p.Number)
            .Select(g => g.First());
        foreach (var product in products)
        {
            // The configuration may hold an instance of another context, so we use the one of the game.
            var item = itemList.FirstOrDefault(i => i.Group == product.Item!.Group && i.Number == product.Item.Number) ?? product.Item!;
            var name = string.IsNullOrWhiteSpace(product.Name) ? GetDefaultName(product, item) : product.Name.Trim();
            var variants = CreateVariants(product, item, name);
            if (variants.Count == 0)
            {
                // E.g. the item has less excellent options than the player should choose.
                continue;
            }

            var tabIndex = IndexOf(tabs, TextOr(product.Tab, DefaultTab));
            var categoryName = TextOr(product.Category, DefaultCategory);
            var categoryIndex = categoryKeys.FindIndex(c => c.TabIndex == tabIndex && SameText(c.Name, categoryName));
            if (categoryIndex < 0)
            {
                categoryKeys.Add((tabIndex, categoryName));
                categoryIndex = categoryKeys.Count - 1;
            }

            var description = string.IsNullOrWhiteSpace(product.Description) ? Describe(product, item) : product.Description.Trim();
            packages.Add(new CashShopPackage(product.Number, FirstCategorySeq + categoryIndex, name, description, variants));
        }

        var categories = tabs.Select((tab, index) => new CashShopCategory(index + 1, tab, null))
            .Concat(categoryKeys.Select((category, index) => new CashShopCategory(FirstCategorySeq + index, category.Name, category.TabIndex + 1)))
            .ToList();
        return new CashShopCatalog(categories, packages, configuration.CurrencyName);
    }

    /// <summary>
    /// Gets a package.
    /// </summary>
    /// <param name="seq">The number of the package.</param>
    /// <returns>The package; <c>null</c>, if it doesn't exist.</returns>
    public CashShopPackage? GetPackage(int seq) => this._packagesBySeq.GetValueOrDefault(seq);

    /// <summary>
    /// Gets the variant which the player bought. The client sends no price number for packages with one variant.
    /// </summary>
    /// <param name="packageSeq">The number of the package.</param>
    /// <param name="priceSeq">The number of the variant, or 0.</param>
    /// <returns>The package and its variant; <c>null</c>, if they don't exist.</returns>
    public (CashShopPackage Package, CashShopVariant Variant)? FindVariant(int packageSeq, int priceSeq)
    {
        if (this.GetPackage(packageSeq) is not { } package)
        {
            return null;
        }

        var variant = priceSeq == 0 && package.Variants.Count == 1
            ? package.Variants[0]
            : package.Variants.FirstOrDefault(v => v.PriceSeq == priceSeq);
        return variant is null ? null : (package, variant);
    }

    /// <summary>
    /// Gets the possible excellent options of an item, ordered by their number.
    /// </summary>
    /// <param name="item">The item definition.</param>
    /// <returns>The excellent options.</returns>
    internal static IReadOnlyList<IncreasableItemOption> GetExcellentOptions(ItemDefinition item)
    {
        return item.PossibleItemOptions
            .SelectMany(o => o.PossibleOptions)
            .Where(o => o.OptionType == ItemOptionTypes.Excellent && o.Number is >= 1 and <= 6)
            .GroupBy(o => o.Number)
            .Select(g => g.First())
            .OrderBy(o => o.Number)
            .ToList();
    }

    /// <summary>
    /// Gets a short Spanish name of an excellent option.
    /// </summary>
    /// <param name="option">The option.</param>
    /// <returns>The name.</returns>
    internal static string GetExcellentOptionName(IncreasableItemOption option)
    {
        var target = option.PowerUpDefinition?.TargetAttribute;
        bool Is(AttributeDefinition attribute) => target is not null && target.Id == attribute.Id;

        if (Is(Stats.ManaAfterMonsterKillMultiplier))
        {
            return "Maná al matar";
        }

        if (Is(Stats.HealthAfterMonsterKillMultiplier))
        {
            return "Vida al matar";
        }

        if (Is(Stats.AttackSpeedAny))
        {
            return "Velocidad +7";
        }

        if (Is(Stats.PhysicalBaseDmgIncrease) || Is(Stats.WizardryBaseDmgIncrease))
        {
            return "Daño +2%";
        }

        if (Is(Stats.PhysicalBaseDmg) || Is(Stats.WizardryBaseDmg))
        {
            return "Daño +nivel/20";
        }

        if (Is(Stats.ExcellentDamageChance))
        {
            return "Daño exc. +10%";
        }

        if (Is(Stats.MoneyAmountRate))
        {
            return "Zen +40%";
        }

        if (Is(Stats.DefenseRatePvm))
        {
            return "Éxito def. +10%";
        }

        if (Is(Stats.DamageReflection))
        {
            return "Refleja 5%";
        }

        if (Is(Stats.ArmorDamageDecrease))
        {
            return "Reduce daño 4%";
        }

        if (Is(Stats.MaximumMana))
        {
            return "Maná +4%";
        }

        if (Is(Stats.MaximumHealth))
        {
            return "Vida +4%";
        }

        return $"Opción {option.Number}";
    }

    private static IReadOnlyList<CashShopVariant> CreateVariants(CashShopProductDefinition product, ItemDefinition item, string name)
    {
        var firstPriceSeq = (product.Number * 100) + 1;
        if (product.Type == CashShopProductType.Skin)
        {
            return [new CashShopVariant(firstPriceSeq, name, product.Price, new CashShopItemSpec(item, 0, 1, false, false, 0, 0, IsSkin: true))];
        }

        var skill = product.Skill && item.Skill is not null;
        if (product.ExcellentOptionChoices == 0)
        {
            var spec = new CashShopItemSpec(item, product.ItemLevel, product.Quantity, skill, product.Luck, product.OptionLevel, product.ExcellentOptions);
            return [new CashShopVariant(firstPriceSeq, name, product.Price, spec)];
        }

        // The player chooses from the options which the item doesn't have anyway.
        var options = GetExcellentOptions(item).Where(o => (product.ExcellentOptions & OptionBit(o)) == 0).ToList();
        return Combinations(options.Count, Math.Min((int)product.ExcellentOptionChoices, CashShopProductDefinition.MaximumExcellentOptions))
            .Select((combination, index) =>
            {
                var mask = (byte)(product.ExcellentOptions | combination.Sum(o => OptionBit(options[o])));
                var variantName = string.Join(" + ", combination.Select(o => GetExcellentOptionName(options[o])));
                var spec = new CashShopItemSpec(item, product.ItemLevel, product.Quantity, skill, product.Luck, product.OptionLevel, mask);
                return new CashShopVariant(firstPriceSeq + index, variantName, product.Price, spec);
            })
            .ToList();
    }

    private static int OptionBit(IncreasableItemOption option) => 1 << (option.Number - 1);

    private static string GetDefaultName(CashShopProductDefinition product, ItemDefinition item)
    {
        if (product.Type == CashShopProductType.Skin)
        {
            return $"Skin {GetName(item)}";
        }

        return (product.Quantity > 1 ? $"{product.Quantity}x " : string.Empty)
               + GetName(item)
               + (product.ItemLevel > 0 ? $" +{product.ItemLevel}" : string.Empty);
    }

    private static string Describe(CashShopProductDefinition product, ItemDefinition item)
    {
        if (product.Type == CashShopProductType.Skin)
        {
            return "Desbloquea la skin en el personaje con el que la canjees. No ocupa lugar en el inventario.";
        }

        var parts = new List<string>();
        if (product.ItemLevel > 0)
        {
            parts.Add($"+{product.ItemLevel}");
        }

        if (product.Skill && item.Skill is not null)
        {
            parts.Add("Skill");
        }

        if (product.Luck)
        {
            parts.Add("Luck");
        }

        if (product.OptionLevel > 0)
        {
            parts.Add($"Opción +{product.OptionLevel * 4}");
        }

        parts.AddRange(GetExcellentOptions(item).Where(o => (product.ExcellentOptions & OptionBit(o)) != 0).Select(GetExcellentOptionName));
        var text = string.Join(", ", parts);
        if (product.ExcellentOptionChoices > 0)
        {
            var choice = product.ExcellentOptionChoices == 1 ? "1 opción excelente" : $"{product.ExcellentOptionChoices} opciones excelentes";
            text += (text.Length > 0 ? ". " : string.Empty) + $"Elegí {choice} al comprar.";
        }

        return text;
    }

    private static string GetName(ItemDefinition item) => item.Name.GetTranslation(CultureInfo.InvariantCulture) ?? $"Item {item.Group}/{item.Number}";

    private static string TextOr(string? text, string fallback) => string.IsNullOrWhiteSpace(text) ? fallback : text.Trim();

    private static bool SameText(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static int IndexOf(List<string> names, string name)
    {
        var index = names.FindIndex(n => SameText(n, name));
        if (index >= 0)
        {
            return index;
        }

        names.Add(name);
        return names.Count - 1;
    }

    private static IEnumerable<int[]> Combinations(int count, int size)
    {
        if (size <= 0 || size > count)
        {
            yield break;
        }

        var indexes = Enumerable.Range(0, size).ToArray();
        while (true)
        {
            yield return (int[])indexes.Clone();
            var i = size - 1;
            while (i >= 0 && indexes[i] == count - size + i)
            {
                i--;
            }

            if (i < 0)
            {
                yield break;
            }

            indexes[i]++;
            for (var j = i + 1; j < size; j++)
            {
                indexes[j] = indexes[j - 1] + 1;
            }
        }
    }
}
