// <copyright file="GameConfigurationHelperTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using MUnique.OpenMU.DataModel;
using BasicModel = MUnique.OpenMU.Persistence.BasicModel;

/// <summary>
/// Tests for <see cref="GameConfigurationHelper"/>.
/// </summary>
[TestFixture]
public class GameConfigurationHelperTests
{
    /// <summary>
    /// A changed item of a merchant store is found in the configuration which the game uses,
    /// so its new values (e.g. the price) can be applied there without a restart.
    /// </summary>
    [Test]
    public void ChangedMerchantStoreItemIsAppliedToTheConfiguration()
    {
        var configuration = new BasicModel.GameConfiguration();
        var storeItem = new BasicModel.Item { Id = Guid.NewGuid(), ItemSlot = 0, StorePrice = 1000 };
        var store = new BasicModel.ItemStorage();
        store.Items.Add(storeItem);
        configuration.Monsters.Add(new BasicModel.MonsterDefinition { MerchantStore = store });

        var changedItem = new BasicModel.Item { Id = storeItem.Id, ItemSlot = 0, StorePrice = 5000, Level = 7 };
        var found = configuration.GetObjectOfConfig((object)changedItem) as IAssignable;
        found?.AssignValuesOf(changedItem, configuration);

        Assert.That(found, Is.SameAs(storeItem));
        Assert.That(storeItem.StorePrice, Is.EqualTo(5000));
        Assert.That(storeItem.Level, Is.EqualTo(7));
    }
}
