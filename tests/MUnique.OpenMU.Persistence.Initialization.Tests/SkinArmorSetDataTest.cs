// <copyright file="SkinArmorSetDataTest.cs" company="MUnique">
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
/// Tests the armor sets of skin packs, which every class can wear.
/// </summary>
[TestFixture]
internal class SkinArmorSetDataTest
{
    private static readonly byte[] ArmorGroups = [7, 8, 9, 10, 11];

    private static readonly object[] Sets =
    [
        new object[] { (short)54, "Pink Gold", new AddPinkGoldSetPlugIn() },
        new object[] { (short)55, "Japanese White", new AddJapaneseWhiteSetPlugIn() },
    ];

    /// <summary>
    /// Tests that a new season 6 database contains the set.
    /// </summary>
    /// <param name="number">The item number of the pieces.</param>
    /// <param name="setName">The name of the set, which starts the names of the pieces.</param>
    /// <param name="update">The update which adds the set.</param>
    [TestCaseSource(nameof(Sets))]
    public async Task NewDatabaseContainsSetAsync(short number, string setName, AddSkinArmorSetPlugInBase update)
    {
        var (_, gameConfiguration) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);

        AssertSet(gameConfiguration, number, setName);
    }

    /// <summary>
    /// Tests that the update adds the same set to a database which was created before it existed.
    /// </summary>
    /// <param name="number">The item number of the pieces.</param>
    /// <param name="setName">The name of the set, which starts the names of the pieces.</param>
    /// <param name="update">The update which adds the set.</param>
    [TestCaseSource(nameof(Sets))]
    public async Task UpdateAddsSetToExistingDatabaseAsync(short number, string setName, AddSkinArmorSetPlugInBase update)
    {
        var (context, gameConfiguration) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);
        using (context)
        {
            bool IsPiece(ItemDefinition item) => item.Number == number && ArmorGroups.Contains(item.Group);

            foreach (var setGroup in gameConfiguration.ItemSetGroups.Where(g => g.Items.Any(i => i.ItemDefinition is { } item && IsPiece(item))).ToList())
            {
                gameConfiguration.ItemSetGroups.Remove(setGroup);
            }

            foreach (var item in gameConfiguration.Items.Where(IsPiece).ToList())
            {
                gameConfiguration.Items.Remove(item);
            }

            Assert.That(gameConfiguration.Items.Any(IsPiece), Is.False);

            await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);

            AssertSet(gameConfiguration, number, setName);
        }
    }

    private static void AssertSet(GameConfiguration gameConfiguration, short number, string setName)
    {
        var pieces = gameConfiguration.Items.Where(i => i.Number == number && ArmorGroups.Contains(i.Group)).OrderBy(i => i.Group).ToList();
        Assert.That(pieces.Select(p => p.Group), Is.EqualTo(ArmorGroups));
        Assert.That(pieces.Select(p => p.Name.Value), Is.EqualTo(new[] { "Helm", "Armor", "Pants", "Gloves", "Boots" }.Select(piece => $"{setName} {piece}")));
        Assert.That(pieces.Select(p => Defense(p)), Is.EqualTo(new[] { 53f, 75f, 65f, 48f, 50f }));

        var allClasses = gameConfiguration.CharacterClasses.Select(c => c.Number).Order().ToList();
        var rageFighters = new[] { (byte)CharacterClassNumber.RageFighter, (byte)CharacterClassNumber.FistMaster };
        var firstWord = setName.Split(' ')[0];
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
            Assert.That(piece.PossibleItemSetGroups.All(g => g.Name.Value!.StartsWith(firstWord + " ", StringComparison.Ordinal)), Is.True, name);
        }
    }

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
