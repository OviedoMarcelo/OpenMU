// <copyright file="BossEncounterDropGenerator.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.BossEncounters;

/// <summary>
/// The drop generator of the boss of an encounter: it drops the configured <see cref="BossEncounterDefinition.Drops"/>
/// and money, in addition to or instead of the drops of the monster.
/// </summary>
/// <remarks>
/// The generated items are placed and owned like every other drop of a monster: around the boss,
/// for the player which killed it and its party.
/// </remarks>
public sealed class BossEncounterDropGenerator : IDropGenerator
{
    private readonly IDropGenerator _monsterDropGenerator;
    private readonly BossEncounterDefinition _definition;
    private readonly IGameContext _gameContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="BossEncounterDropGenerator"/> class.
    /// </summary>
    /// <param name="monsterDropGenerator">The drop generator of the monsters.</param>
    /// <param name="definition">The definition of the encounter.</param>
    /// <param name="gameContext">The game context.</param>
    public BossEncounterDropGenerator(IDropGenerator monsterDropGenerator, BossEncounterDefinition definition, IGameContext gameContext)
    {
        this._monsterDropGenerator = monsterDropGenerator;
        this._definition = definition;
        this._gameContext = gameContext;
    }

    /// <inheritdoc />
    public async ValueTask<(IEnumerable<Item> Items, uint? Money)> GenerateItemDropsAsync(MonsterDefinition monster, int gainedExperience, Player player)
    {
        var items = new List<Item>();
        uint? money = null;
        if (!this._definition.ReplaceMonsterDrops)
        {
            (var monsterItems, money) = await this._monsterDropGenerator.GenerateItemDropsAsync(monster, gainedExperience, player).ConfigureAwait(false);
            items.AddRange(monsterItems);
        }

        items.AddRange(this.GenerateConfiguredItems());
        if (this._definition.DropMoney > 0)
        {
            money = (money ?? 0) + (uint)this._definition.DropMoney;
        }

        return (items, money);
    }

    /// <inheritdoc />
    public Item? GenerateItemDrop(DropItemGroup group)
    {
        return this._monsterDropGenerator.GenerateItemDrop(group);
    }

    /// <inheritdoc />
    public (Item? Item, uint? Money, ItemDropEffect DropEffect) GenerateItemDrop(IEnumerable<DropItemGroup> groups)
    {
        return this._monsterDropGenerator.GenerateItemDrop(groups);
    }

    /// <summary>
    /// Generates the configured items. The chance of each drop applies to each of its items.
    /// </summary>
    /// <returns>The generated items.</returns>
    internal IEnumerable<Item> GenerateConfiguredItems()
    {
        foreach (var drop in this._definition.Drops)
        {
            if (ConfiguredItemFactory.Resolve(this._gameContext, drop.Item) is not { } itemDefinition)
            {
                continue;
            }

            for (var i = 0; i < drop.Quantity; i++)
            {
                if (Rand.NextRandomBool(drop.Chance / 100.0))
                {
                    yield return ConfiguredItemFactory.Create(itemDefinition, drop.ItemLevel, drop.Skill, drop.Luck, drop.OptionLevel, drop.ExcellentOptions);
                }
            }
        }
    }
}
