// <copyright file="PinkGoldSetDataTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Persistence.Initialization.CharacterClasses;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.InMemory;

/// <summary>
/// Tests the Pink Gold set, the armor set which every class can wear.
/// </summary>
[TestFixture]
internal class PinkGoldSetDataTest
{
    private const short PinkGoldNumber = 54;

    private static readonly byte[] ArmorGroups = [7, 8, 9, 10, 11];

    /// <summary>
    /// Tests that a new season 6 database contains the Pink Gold set.
    /// </summary>
    [Test]
    public async Task NewDatabaseContainsPinkGoldSetAsync()
    {
        var (_, gameConfiguration) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);

        AssertPinkGoldSet(gameConfiguration);
    }

    /// <summary>
    /// Tests that the update adds the same Pink Gold set to a database which was created before it existed.
    /// </summary>
    [Test]
    public async Task UpdateAddsPinkGoldSetToExistingDatabaseAsync()
    {
        var (context, gameConfiguration) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);
        using (context)
        {
            var pinkGoldItems = gameConfiguration.Items.Where(IsPinkGoldPiece).ToList();
            foreach (var setGroup in gameConfiguration.ItemSetGroups.Where(g => g.Items.Any(i => i.ItemDefinition is { } item && IsPinkGoldPiece(item))).ToList())
            {
                gameConfiguration.ItemSetGroups.Remove(setGroup);
            }

            foreach (var item in pinkGoldItems)
            {
                gameConfiguration.Items.Remove(item);
            }

            Assert.That(gameConfiguration.Items.Any(IsPinkGoldPiece), Is.False);

            await new AddPinkGoldSetPlugIn().ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);

            AssertPinkGoldSet(gameConfiguration);
        }
    }

    private static void AssertPinkGoldSet(GameConfiguration gameConfiguration)
    {
        var pieces = gameConfiguration.Items.Where(IsPinkGoldPiece).OrderBy(i => i.Group).ToList();
        Assert.That(pieces.Select(p => p.Group), Is.EqualTo(ArmorGroups));
        Assert.That(pieces.Select(p => p.Name.Value), Is.EqualTo(new[] { "Pink Gold Helm", "Pink Gold Armor", "Pink Gold Pants", "Pink Gold Gloves", "Pink Gold Boots" }));
        Assert.That(pieces.Select(p => Defense(p)), Is.EqualTo(new[] { 53f, 75f, 65f, 48f, 50f }));

        var allClasses = gameConfiguration.CharacterClasses.Select(c => c.Number).Order().ToList();
        var rageFighters = new[] { (byte)CharacterClassNumber.RageFighter, (byte)CharacterClassNumber.FistMaster };
        foreach (var piece in pieces)
        {
            var name = piece.Name.Value;

            // Every class, except the rage fighter for the gloves.
            var expectedClasses = piece.Group == 10 ? allClasses.Except(rageFighters).ToList() : allClasses;
            Assert.That(piece.QualifiedCharacters.Select(c => c.Number).Order(), Is.EqualTo(expectedClasses), name);
            Assert.That(piece.Requirements, Is.Empty, name);
            Assert.That(piece.PossibleItemOptions.Any(o => o.PossibleOptions.Any(p => p.OptionType == ItemOptionTypes.Excellent)), Is.True, name);

            // The defense rate bonus (any level) and the defense bonus of each set level from 10 to 15.
            Assert.That(piece.PossibleItemSetGroups, Has.Count.EqualTo(7), name);
            Assert.That(piece.PossibleItemSetGroups.All(g => g.MinimumItemCount == 5 && g.Items.Count == 5), Is.True, name);
        }
    }

    private static bool IsPinkGoldPiece(ItemDefinition item) => item.Number == PinkGoldNumber && ArmorGroups.Contains(item.Group);

    private static float Defense(ItemDefinition item) => item.BasePowerUpAttributes.Single(p => p.TargetAttribute?.Id == Stats.DefenseBase.Id).BaseValue;

    private static async Task<(IContext Context, GameConfiguration GameConfiguration)> CreateSeason6ConfigurationAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        var dataInitialization = new VersionSeasonSix.DataInitialization(contextProvider, new NullLoggerFactory());
        await dataInitialization.CreateInitialDataAsync(1, true).ConfigureAwait(false);

        var context = contextProvider.CreateNewContext();
        return (context, (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First());
    }
}
