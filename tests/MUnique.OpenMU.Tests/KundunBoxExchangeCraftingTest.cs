// <copyright file="KundunBoxExchangeCraftingTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using System.IO;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlayerActions.Craftings;
using MUnique.OpenMU.GameLogic.Views.NPC;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using Moq;

/// <summary>
/// Tests the <see cref="KundunBoxExchangeCrafting"/>.
/// </summary>
[TestFixture]
public class KundunBoxExchangeCraftingTest
{
    private static readonly ItemDefinition PackedCreation = new() { Group = 12, Number = 137, Width = 1, Height = 1 };

    /// <summary>
    /// Tests the points which an item is worth by its drop level.
    /// </summary>
    /// <param name="dropLevel">The drop level.</param>
    /// <returns>The points.</returns>
    [TestCase(10, ExpectedResult = 1)]
    [TestCase(39, ExpectedResult = 1)]
    [TestCase(40, ExpectedResult = 2)]
    [TestCase(79, ExpectedResult = 2)]
    [TestCase(80, ExpectedResult = 3)]
    public int PointsDependOnTheDropLevel(int dropLevel)
    {
        return KundunBoxExchangeCrafting.GetPoints(Excellent(dropLevel));
    }

    /// <summary>
    /// Tests that items which aren't plain excellent equipment are worth nothing.
    /// </summary>
    [Test]
    public void OtherItemsAreWorthNothing()
    {
        var notExcellent = CreateItem(new ItemDefinition { Group = 0, DropLevel = 100 });
        var wings = Excellent(100);
        wings.Definition!.Group = 12;
        var socket = Excellent(100);
        socket.SocketCount = 1;

        Assert.That(KundunBoxExchangeCrafting.GetPoints(notExcellent), Is.Zero);
        Assert.That(KundunBoxExchangeCrafting.GetPoints(wings), Is.Zero);
        Assert.That(KundunBoxExchangeCrafting.GetPoints(socket), Is.Zero);
    }

    /// <summary>
    /// Tests the combinations of items which are accepted or refused.
    /// </summary>
    /// <param name="dropLevels">The drop levels of the excellent items.</param>
    /// <returns>The result of the check; <c>null</c> when it's accepted.</returns>
    [TestCase(new[] { 80, 80 }, ExpectedResult = null)]
    [TestCase(new[] { 40, 40, 40 }, ExpectedResult = null)]
    [TestCase(new[] { 10, 10, 10, 10, 10, 10 }, ExpectedResult = null)]
    [TestCase(new[] { 80, 40, 40 }, ExpectedResult = null)]
    [TestCase(new[] { 80, 40 }, ExpectedResult = CraftingResult.LackingMixItems)]
    [TestCase(new[] { 80, 80, 10 }, ExpectedResult = CraftingResult.TooManyItems)]
    public async Task<CraftingResult?> ItemsAreCheckedByTheirPointsAsync(int[] dropLevels)
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        byte slot = 0;
        foreach (var dropLevel in dropLevels)
        {
            await player.TemporaryStorage!.AddItemAsync(slot++, Excellent(dropLevel)).ConfigureAwait(false);
        }

        await player.TemporaryStorage!.AddItemAsync(slot, CreateItem(PackedCreation)).ConfigureAwait(false);
        return new KundunBoxExchangeCrafting().TryGetRequiredItems(player, out _, out _);
    }

    /// <summary>
    /// Tests that exactly one pack of ten is needed.
    /// </summary>
    /// <param name="packs">The amount of packs.</param>
    /// <param name="packLevel">The level of the packs.</param>
    [TestCase(0, 0)]
    [TestCase(2, 0)]
    [TestCase(1, 1)]
    public async Task OnlyOnePackOfTenIsAcceptedAsync(int packs, int packLevel)
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        await player.TemporaryStorage!.AddItemAsync(0, Excellent(80)).ConfigureAwait(false);
        await player.TemporaryStorage!.AddItemAsync(1, Excellent(80)).ConfigureAwait(false);
        for (byte i = 0; i < packs; i++)
        {
            await player.TemporaryStorage!.AddItemAsync((byte)(2 + i), CreateItem(PackedCreation, (byte)packLevel)).ConfigureAwait(false);
        }

        var result = new KundunBoxExchangeCrafting().TryGetRequiredItems(player, out _, out _);

        Assert.That(result, Is.EqualTo(CraftingResult.IncorrectMixItems));
    }

    /// <summary>
    /// Tests that the Coleccionista de Excelentes stands on a free place of the safezone of Elvenland.
    /// </summary>
    [Test]
    public void TheNpcStandsOnAWalkablePlace()
    {
        var assembly = typeof(AddKundunCollectorPlugIn).Assembly;
        var resourceName = assembly.GetManifestResourceNames().Single(name => name.EndsWith(".Terrain52.att", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new BinaryReader(stream);
        var terrain = new GameMapTerrain(new GameMapDefinition { Number = 51, TerrainData = reader.ReadBytes(3 * ushort.MaxValue) });
        var (x, y) = (48, 228);

        Assert.That(terrain.WalkMap[x, y], Is.True);
        Assert.That(terrain.SafezoneMap[x, y], Is.True);
    }

    private static Item Excellent(int dropLevel)
    {
        var item = CreateItem(new ItemDefinition { Group = 0, Width = 1, Height = 1, DropLevel = (byte)dropLevel });
        item.ItemOptions.Add(new ItemOptionLink { ItemOption = new IncreasableItemOption { OptionType = ItemOptionTypes.Excellent } });
        return item;
    }

    private static Item CreateItem(ItemDefinition definition, byte level = 0)
    {
        var itemMock = new Mock<Item>();
        itemMock.SetupAllProperties();
        itemMock.Setup(i => i.ItemOptions).Returns(new List<ItemOptionLink>());
        itemMock.Setup(i => i.ItemSetGroups).Returns(new List<ItemOfItemSet>());
        var item = itemMock.Object;
        item.Definition = definition;
        item.Level = level;
        return item;
    }
}
