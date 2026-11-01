// <copyright file="KundunBoxExchangeCrafting.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlayerActions.Craftings;

using MUnique.OpenMU.DataModel.Configuration.ItemCrafting;
using MUnique.OpenMU.GameLogic.PlayerActions.Items;
using MUnique.OpenMU.GameLogic.Views.NPC;

/// <summary>
/// The exchange of excellent items and a pack of Jewels of Creation for a Box of Kundun +5.
/// Better excellent items are worth more points, so that fewer of them are needed.
/// </summary>
/// <remarks>
/// The client shows the points with the same values, see CMixRecipeMgr in MuMain.
/// </remarks>
public class KundunBoxExchangeCrafting : BaseItemCraftingHandler
{
    /// <summary>
    /// The points of excellent items which are needed for a box.
    /// </summary>
    internal const int RequiredPoints = 6;

    /// <summary>
    /// The drop level from which an excellent item is worth two points.
    /// </summary>
    internal const int MediumItemDropLevel = 40;

    /// <summary>
    /// The drop level from which an excellent item is worth three points.
    /// </summary>
    internal const int GoodItemDropLevel = 80;

    private const byte LastEquipmentGroup = 11;

    private const byte PackedCreationGroup = 12;

    private const short PackedCreationNumber = 137;

    private const byte BoxOfLuckGroup = 14;

    private const short BoxOfLuckNumber = 11;

    /// <summary>
    /// The item level of the Box of Luck which makes it a Box of Kundun +5.
    /// </summary>
    private const byte KundunPlusFiveLevel = 12;

    /// <summary>
    /// Gets the points which an item is worth in the exchange.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>The points of the item; 0, if the item can't be exchanged.</returns>
    internal static int GetPoints(Item item)
    {
        if (item.Definition is not { } definition
            || definition.Group > LastEquipmentGroup
            || !item.IsExcellent()
            || item.IsAncient()
            || item.SocketCount > 0)
        {
            return 0;
        }

        return definition.DropLevel switch
        {
            >= GoodItemDropLevel => 3,
            >= MediumItemDropLevel => 2,
            _ => 1,
        };
    }

    /// <inheritdoc />
    public override CraftingResult? TryGetRequiredItems(Player player, out IList<CraftingRequiredItemLink> items, out byte successRateByItems)
    {
        successRateByItems = 0;
        items = new List<CraftingRequiredItemLink>(2);

        var excellentItems = new List<Item>();
        var packs = new List<Item>();
        foreach (var item in player.TemporaryStorage!.Items)
        {
            if (IsPackOfTenCreation(item))
            {
                packs.Add(item);
            }
            else if (GetPoints(item) > 0)
            {
                excellentItems.Add(item);
            }
            else
            {
                return CraftingResult.IncorrectMixItems;
            }
        }

        if (packs.Count != 1 || excellentItems.Count == 0)
        {
            return CraftingResult.IncorrectMixItems;
        }

        var points = excellentItems.Sum(GetPoints);
        if (points < RequiredPoints)
        {
            return CraftingResult.LackingMixItems;
        }

        // The exchange takes everything, so it's refused when an item would be given away for nothing.
        if (points - excellentItems.Min(GetPoints) >= RequiredPoints)
        {
            return CraftingResult.TooManyItems;
        }

        items.Add(new CraftingRequiredItemLink(
            excellentItems,
            new TransientItemCraftingRequiredItem
            {
                MinimumAmount = (byte)excellentItems.Count,
                MaximumAmount = (byte)excellentItems.Count,
                SuccessResult = MixResult.Disappear,
            }));
        items.Add(new CraftingRequiredItemLink(
            packs,
            new TransientItemCraftingRequiredItem
            {
                PossibleItems = { packs[0].Definition! },
                MinimumAmount = 1,
                MaximumAmount = 1,
                SuccessResult = MixResult.Disappear,
            }));

        successRateByItems = 100;
        return default;
    }

    /// <inheritdoc />
    protected override int GetPrice(byte successRate, IList<CraftingRequiredItemLink> requiredItems) => 0;

    /// <inheritdoc />
    protected override async ValueTask<List<Item>> CreateOrModifyResultItemsAsync(IList<CraftingRequiredItemLink> requiredItems, Player player, byte socketSlot, byte successRate)
    {
        var box = player.PersistenceContext.CreateNew<Item>();
        box.Definition = player.GameContext.Configuration.Items.First(i => i.Group == BoxOfLuckGroup && i.Number == BoxOfLuckNumber);
        box.Level = KundunPlusFiveLevel;
        box.Durability = 1;
        await player.TemporaryStorage!.AddItemAsync(box).ConfigureAwait(false);
        return [box];
    }

    /// <summary>
    /// Determines whether the item is a pack of ten Jewels of Creation: the higher levels are packs of 20 and 30.
    /// </summary>
    private static bool IsPackOfTenCreation(Item item)
    {
        return item.Definition is { Group: PackedCreationGroup, Number: PackedCreationNumber } && item.Level == 0;
    }
}
