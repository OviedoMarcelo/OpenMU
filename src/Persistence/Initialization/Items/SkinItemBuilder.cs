// <copyright file="SkinItemBuilder.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Items;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;

/// <summary>
/// Builds the skins of skin packs which are a single item: wings (group 12) and pets (group 13). Like the armor sets
/// of <see cref="SkinArmorSetBuilder"/>, they are only skins, worn as cosmetic pieces: the item has no stats, every
/// class can wear it and it doesn't drop from monsters.
/// </summary>
public static class SkinItemBuilder
{
    /// <summary>
    /// The item group of the wings.
    /// </summary>
    public const byte WingsGroup = 12;

    /// <summary>
    /// The item group of the pets and mounts.
    /// </summary>
    public const byte PetGroup = 13;

    /// <summary>
    /// The lowest number of the skins of wings and pets. The numbers below are used by the items of the game, in the
    /// server and in the client.
    /// </summary>
    public const short FirstNumber = 200;

    /// <summary>
    /// The wings whose size and slot the wing skins take: the Wings of Elf.
    /// </summary>
    public const short WingsTemplateNumber = 0;

    /// <summary>
    /// Adds the item of a skin, if it doesn't exist yet, and takes the stats off it, if it exists.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    /// <param name="group">The item group: <see cref="WingsGroup"/> or <see cref="PetGroup"/>.</param>
    /// <param name="number">The item number.</param>
    /// <param name="name">The name of the item.</param>
    /// <param name="template">The item whose size and slot the skin takes, e.g. the pet it's a skin of.</param>
    /// <returns>The item.</returns>
    public static ItemDefinition Build(IContext context, GameConfiguration gameConfiguration, byte group, short number, string name, ItemDefinition template)
    {
        if (SkinArmorSetBuilder.FindItem(gameConfiguration, group, number) is { } existing)
        {
            SkinArmorSetBuilder.MakeSkinOnly(existing);
            return existing;
        }

        var item = context.CreateNew<ItemDefinition>();
        item.Group = group;
        item.Number = number;
        item.Name = name;
        item.Width = template.Width;
        item.Height = template.Height;
        item.MaximumItemLevel = template.MaximumItemLevel;
        item.Durability = template.Durability;
        item.ItemSlot = template.ItemSlot;
        item.DropsFromMonsters = false;
        item.SetGuid(item.Group, item.Number);
        foreach (var characterClass in gameConfiguration.CharacterClasses)
        {
            item.QualifiedCharacters.Add(characterClass);
        }

        gameConfiguration.Items.Add(item);
        return item;
    }
}
