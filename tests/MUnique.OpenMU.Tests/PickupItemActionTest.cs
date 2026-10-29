// <copyright file="PickupItemActionTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Moq;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.PlayerActions.Items;
using MUnique.OpenMU.GameLogic.Views;
using MUnique.OpenMU.Interfaces;
using NUnit.Framework;

/// <summary>
/// Tests for the <see cref="PickupItemAction"/>.
/// </summary>
[TestFixture]
public class PickupItemActionTest
{
    /// <summary>
    /// Tests that a player without room for a dropped item is told so, instead of a silent failure which looked like
    /// the item belonged to someone else (e.g. a weapon out of a box, with an inventory full of boxes).
    /// </summary>
    [Test]
    public async Task PlayerWithoutRoomIsToldAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.Attributes![Stats.CurrentHealth] = 100;
        var definition = new Mock<ItemDefinition>();
        definition.SetupAllProperties();
        definition.Setup(d => d.PossibleItemOptions).Returns(new List<ItemOptionDefinition>());
        definition.Setup(d => d.BasePowerUpAttributes).Returns(new List<ItemBasePowerUpDefinition>());

        // Larger than the inventory, so it never fits.
        definition.Object.Width = 9;
        definition.Object.Height = 9;
        definition.Object.Durability = 255;
        var item = new TemporaryItem { Definition = definition.Object, Durability = 255 };
        var droppedItem = new DroppedItem(item, player.Position, player.CurrentMap!, null, null);
        await player.CurrentMap!.AddAsync(droppedItem).ConfigureAwait(false);

        await new PickupItemAction().PickupItemAsync(player, droppedItem.Id).ConfigureAwait(false);

        Assert.That(player.Inventory!.Items, Is.Empty);
        Mock.Get(player.ViewPlugIns.GetPlugIn<IShowMessagePlugIn>()!)
            .Verify(p => p.ShowMessageAsync(It.IsAny<string>(), It.IsAny<MessageType>()), Times.Once);
    }
}
