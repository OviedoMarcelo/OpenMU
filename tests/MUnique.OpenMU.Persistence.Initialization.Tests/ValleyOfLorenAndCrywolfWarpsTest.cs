// <copyright file="ValleyOfLorenAndCrywolfWarpsTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.InMemory;

/// <summary>
/// Tests the warps of Valley of Loren and Crywolf in the move window, and the index of Vulcanus.
/// </summary>
[TestFixture]
internal class ValleyOfLorenAndCrywolfWarpsTest
{
    /// <summary>
    /// Tests that a new season 6 database has the warps, with the indexes of the MoveReq of the client.
    /// </summary>
    [Test]
    public async Task NewDatabaseContainsWarpsAsync()
    {
        var (_, gameConfiguration) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);

        AssertWarps(gameConfiguration);
    }

    /// <summary>
    /// Tests that the update adds the warps and moves Vulcanus in a database which was created before.
    /// </summary>
    [Test]
    public async Task UpdateAddsWarpsToExistingDatabaseAsync()
    {
        var (context, gameConfiguration) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);
        using (context)
        {
            // As it was before: without the two warps, and Vulcanus at 37.
            foreach (var warp in gameConfiguration.WarpList.Where(w => w.Index is 49 or 50).ToList())
            {
                gameConfiguration.WarpList.Remove(warp);
            }

            gameConfiguration.WarpList.Single(w => w.Name.Value == "Vulcanus").Index = 37;

            var update = new AddValleyOfLorenAndCrywolfWarpsPlugIn();
            await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
            AssertWarps(gameConfiguration);

            // Applying it again doesn't add them twice.
            await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
            Assert.That(gameConfiguration.WarpList.Count(w => w.Index is 49 or 50), Is.EqualTo(2));
        }
    }

    private static void AssertWarps(GameConfiguration gameConfiguration)
    {
        var valley = gameConfiguration.WarpList.Single(w => w.Index == 49);
        Assert.That(valley.Name.Value, Is.EqualTo("ValleyOfLoren"));
        Assert.That(valley.Costs, Is.EqualTo(10000));
        Assert.That(valley.LevelRequirement, Is.EqualTo(10));
        Assert.That(valley.Gate?.Map?.Number, Is.EqualTo(30));
        Assert.That((valley.Gate!.X1, valley.Gate.Y1), Is.EqualTo(((byte)29, (byte)37)));

        var crywolf = gameConfiguration.WarpList.Single(w => w.Index == 50);
        Assert.That(crywolf.Name.Value, Is.EqualTo("Crywolf"));
        Assert.That(crywolf.Costs, Is.EqualTo(15000));
        Assert.That(crywolf.LevelRequirement, Is.EqualTo(150));
        Assert.That(crywolf.Gate?.Map?.Number, Is.EqualTo(34));
        Assert.That((crywolf.Gate!.X1, crywolf.Gate.Y1), Is.EqualTo(((byte)231, (byte)37)));

        Assert.That(gameConfiguration.WarpList.Single(w => w.Name.Value == "Vulcanus").Index, Is.EqualTo(42));
        Assert.That(gameConfiguration.WarpList.Any(w => w.Index == 37), Is.False);
    }

    private static async Task<(IContext Context, GameConfiguration GameConfiguration)> CreateSeason6ConfigurationAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        var dataInitialization = new VersionSeasonSix.DataInitialization(contextProvider, new NullLoggerFactory());
        await dataInitialization.CreateInitialDataAsync(1, true).ConfigureAwait(false);

        var context = contextProvider.CreateNewContext();
        return (context, (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First());
    }
}
