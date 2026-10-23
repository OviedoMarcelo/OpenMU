// <copyright file="IceColossusDataTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.InMemory;

/// <summary>
/// Tests the data of the Coloso de Hielo, a custom boss.
/// </summary>
[TestFixture]
internal class IceColossusDataTest
{
    private const short Number = 700;

    /// <summary>
    /// Tests that a new season 6 database contains the Coloso de Hielo, without a spawn and drops of its own.
    /// </summary>
    [Test]
    public async Task NewDatabaseContainsIceColossusAsync()
    {
        var (_, gameConfiguration) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);

        AssertIceColossus(gameConfiguration);
        Assert.That(gameConfiguration.Maps.SelectMany(m => m.MonsterSpawns).Any(s => s.MonsterDefinition?.Number == Number), Is.False);
    }

    /// <summary>
    /// Tests that the update adds the Coloso de Hielo to a database which was created before it existed, only once.
    /// </summary>
    [Test]
    public async Task UpdateAddsIceColossusOnceAsync()
    {
        var (context, gameConfiguration) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);
        using (context)
        {
            gameConfiguration.Monsters.Remove(gameConfiguration.Monsters.Single(m => m.Number == Number));

            await new AddIceColossusPlugIn().ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
            await new AddIceColossusPlugIn().ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);

            AssertIceColossus(gameConfiguration);
        }
    }

    private static void AssertIceColossus(GameConfiguration gameConfiguration)
    {
        var monster = gameConfiguration.Monsters.Single(m => m.Number == Number);
        Assert.That(monster.Designation.Value, Is.EqualTo("Coloso de Hielo"));
        Assert.That(Attribute(monster, Stats.Level), Is.EqualTo(150));
        Assert.That(Attribute(monster, Stats.MaximumHealth), Is.EqualTo(150_000));
        Assert.That(monster.DropItemGroups, Is.Empty);
    }

    private static float Attribute(MonsterDefinition monster, AttributeSystem.AttributeDefinition attribute) =>
        monster.Attributes.Single(a => a.AttributeDefinition?.Id == attribute.Id).Value;

    private static async Task<(IContext Context, GameConfiguration GameConfiguration)> CreateSeason6ConfigurationAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        var dataInitialization = new VersionSeasonSix.DataInitialization(contextProvider, new NullLoggerFactory());
        await dataInitialization.CreateInitialDataAsync(1, true).ConfigureAwait(false);

        var context = contextProvider.CreateNewContext();
        return (context, (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First());
    }
}
