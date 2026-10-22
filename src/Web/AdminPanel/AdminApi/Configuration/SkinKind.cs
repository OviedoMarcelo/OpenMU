// <copyright file="SkinKind.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AdminApi.Configuration;

using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic.PlugIns.Cosmetics;
using MUnique.OpenMU.Persistence.Initialization.Items;

/// <summary>
/// A kind of skin of the <see cref="SkinSetsController"/>: an armor set (five pieces), wings or a pet.
/// </summary>
/// <param name="Key">The key of the kind in the routes and requests.</param>
/// <param name="Slots">The cosmetic slots of its pieces.</param>
/// <param name="FirstNumber">The lowest item number which a new skin of the kind gets.</param>
/// <param name="CosmeticNumberBase">The base of the numbers of the cosmetic pieces of single item skins (e.g. 13000 for the pet 13000 + number); 0 for the armor sets.</param>
internal sealed record SkinKind(string Key, IReadOnlyList<CosmeticSlot> Slots, short FirstNumber, short CosmeticNumberBase)
{
    /// <summary>
    /// The cosmetic pieces of an armor set get the item number times this, plus 1 for the helm to 5 for the boots, e.g. 551 to 555 for the set 55.
    /// </summary>
    private const int CosmeticNumbersPerSet = 10;

    /// <summary>
    /// The armor sets: a helm, armor, pants, gloves and boots with the same number.
    /// </summary>
    public static readonly SkinKind Armor = new("armor", CosmeticSlotExtensions.ArmorSlots, 0, 0);

    /// <summary>
    /// The wings.
    /// </summary>
    public static readonly SkinKind Wings = new("wings", [CosmeticSlot.Wings], SkinItemBuilder.FirstNumber, 12000);

    /// <summary>
    /// The pets and mounts. A pet skin is only shown over the pet it's a skin of.
    /// </summary>
    public static readonly SkinKind Pet = new("pet", [CosmeticSlot.Pet], SkinItemBuilder.FirstNumber, 13000);

    /// <summary>
    /// Gets all kinds.
    /// </summary>
    public static IReadOnlyList<SkinKind> All { get; } = [Armor, Wings, Pet];

    /// <summary>
    /// Gets the item groups of the pieces.
    /// </summary>
    public IReadOnlyList<byte> Groups => this.Slots.Select(s => s.GetItemGroup()).ToList();

    /// <summary>
    /// Gets a value indicating whether the skin is a single item, i.e. wings or a pet.
    /// </summary>
    public bool IsSingleItem => this.Slots.Count == 1;

    /// <summary>
    /// Finds the kind with the key.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The kind; <c>null</c>, if there is none.</returns>
    public static SkinKind? Find(string? key) => All.FirstOrDefault(k => string.Equals(k.Key, key, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Gets the name of a piece, e.g. "Toranaga Helm" of the armor set "Toranaga", or the name itself for wings and pets.
    /// </summary>
    /// <param name="name">The name of the skin.</param>
    /// <param name="group">The item group of the piece.</param>
    /// <returns>The name of the piece.</returns>
    public string GetPieceName(string name, byte group) => this.IsSingleItem ? name : SkinArmorSetBuilder.GetPieceName(name, group);

    /// <summary>
    /// Gets the name of a skin from its pieces, e.g. "Toranaga" from "Toranaga Armor".
    /// </summary>
    /// <param name="pieces">The pieces.</param>
    /// <returns>The name.</returns>
    public string GetName(IEnumerable<ItemDefinition?> pieces)
    {
        var list = pieces.OfType<ItemDefinition>().ToList();
        if (this.IsSingleItem)
        {
            return list.FirstOrDefault()?.Name.Value ?? string.Empty;
        }

        var armor = list.FirstOrDefault(p => p.Group == CosmeticSlot.Armor.GetItemGroup())?.Name.Value ?? string.Empty;
        const string suffix = " Armor";
        return armor.EndsWith(suffix, StringComparison.Ordinal) ? armor[..^suffix.Length] : armor;
    }

    /// <summary>
    /// Gets the number which the cosmetic piece of a slot gets, if it's free.
    /// </summary>
    /// <param name="number">The item number of the skin.</param>
    /// <param name="slot">The slot.</param>
    /// <returns>The number of the cosmetic piece.</returns>
    public short GetCosmeticNumber(short number, CosmeticSlot slot) =>
        this.IsSingleItem ? (short)(this.CosmeticNumberBase + number) : (short)((number * CosmeticNumbersPerSet) + (int)slot + 1);
}
