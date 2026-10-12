// <copyright file="ConfiguredItemFactory.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns;

using MUnique.OpenMU.DataModel.Configuration.Items;

/// <summary>
/// Creates the items which are configured in plugins, e.g. as reward of a quest or as drop of a boss.
/// </summary>
internal static class ConfiguredItemFactory
{
    /// <summary>
    /// Gets the item definition of the game, which matches the configured one.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <param name="configured">The configured item definition.</param>
    /// <returns>The item definition; <c>null</c>, if none is configured.</returns>
    public static ItemDefinition? Resolve(IGameContext gameContext, ItemDefinition? configured)
    {
        if (configured is null)
        {
            return null;
        }

        // The configuration of the plugin may hold an instance of another context, so we use the one of the game.
        return gameContext.Configuration.Items
                   .FirstOrDefault(i => i.Group == configured.Group && i.Number == configured.Number)
               ?? configured;
    }

    /// <summary>
    /// Creates an item with the specified options.
    /// </summary>
    /// <param name="itemDefinition">The item definition.</param>
    /// <param name="level">The level, which is limited to the maximum level of the item.</param>
    /// <param name="skill">If set to <c>true</c>, the item has its skill, if it has one.</param>
    /// <param name="luck">If set to <c>true</c>, the item has luck, if it can have it.</param>
    /// <param name="optionLevel">The level of the additional option, 0 for none.</param>
    /// <param name="excellentOptions">The excellent options as bit mask, like the /item command: 1, 2, 4, 8, 16, 32.</param>
    /// <returns>The created item.</returns>
    public static TemporaryItem Create(ItemDefinition itemDefinition, byte level, bool skill, bool luck, byte optionLevel, byte excellentOptions)
    {
        var item = new TemporaryItem
        {
            Definition = itemDefinition,
            Level = Math.Min(level, itemDefinition.MaximumItemLevel),
            SocketCount = itemDefinition.MaximumSockets,
        };
        item.Durability = item.IsStackable() ? 1 : itemDefinition.Durability;
        item.HasSkill = itemDefinition.Skill is not null && skill;

        var possibleOptions = itemDefinition.PossibleItemOptions.SelectMany(o => o.PossibleOptions).ToList();
        if (optionLevel > 0
            && possibleOptions.FirstOrDefault(o => o.OptionType == ItemOptionTypes.Option) is { } option)
        {
            item.ItemOptions.Add(new ItemOptionLink { ItemOption = option, Level = optionLevel });
        }

        if (luck
            && possibleOptions.FirstOrDefault(o => o.OptionType == ItemOptionTypes.Luck) is { } luckOption)
        {
            item.ItemOptions.Add(new ItemOptionLink { ItemOption = luckOption });
        }

        if (excellentOptions > 0)
        {
            var options = possibleOptions
                .Where(o => o.OptionType == ItemOptionTypes.Excellent)
                .Where(o => ((1 << (o.Number - 1)) & excellentOptions) > 0)
                .ToList();
            foreach (var excellentOption in options)
            {
                item.ItemOptions.Add(new ItemOptionLink { ItemOption = excellentOption });
            }

            // Like the /item command: every excellent item has its skill, if the item definition has one.
            item.HasSkill |= options.Count > 0 && itemDefinition.Skill is not null;
        }

        return item;
    }
}
