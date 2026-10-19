// <copyright file="SkinArmorSetDataTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Persistence.Initialization.CharacterClasses;
using MUnique.OpenMU.Persistence.Initialization.Items;
using MUnique.OpenMU.Persistence.InMemory;

/// <summary>
/// Tests the <see cref="SkinArmorSetBuilder"/>, which builds the armor sets of skin packs.
/// </summary>
[TestFixture]
internal class SkinArmorSetDataTest
{
    private const short Number = 54;

    private const string SetName = "Pink Gold";

    private static readonly byte[] ArmorGroups = [7, 8, 9, 10, 11];

    /// <summary>
    /// Tests that the builder builds the whole set.
    /// </summary>
    [Test]
    public async Task BuildsTheSetAsync()
    {
        var (context, gameConfiguration) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);
        using (context)
        {
            SkinArmorSetBuilder.Build(context, gameConfiguration, Number, SetName);

            AssertSet(gameConfiguration);
        }
    }

    /// <summary>
    /// Tests that building the set again doesn't add anything.
    /// </summary>
    [Test]
    public async Task BuildingAgainAddsNothingAsync()
    {
        var (context, gameConfiguration) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);
        using (context)
        {
            SkinArmorSetBuilder.Build(context, gameConfiguration, Number, SetName);
            var itemCount = gameConfiguration.Items.Count;
            var setGroupCount = gameConfiguration.ItemSetGroups.Count;

            SkinArmorSetBuilder.Build(context, gameConfiguration, Number, SetName);

            Assert.That(gameConfiguration.Items, Has.Count.EqualTo(itemCount));
            Assert.That(gameConfiguration.ItemSetGroups, Has.Count.EqualTo(setGroupCount));
            AssertSet(gameConfiguration);
        }
    }

    /// <summary>
    /// Tests that building a set which was interrupted after its pieces completes it.
    /// </summary>
    [Test]
    public async Task CompletesAnInterruptedSetAsync()
    {
        var (context, gameConfiguration) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);
        using (context)
        {
            var pieces = SkinArmorSetBuilder.AddMissingPieces(context, gameConfiguration, Number, SetName);
            Assert.That(pieces.All(p => p.PossibleItemSetGroups.Count == 0), Is.True);

            SkinArmorSetBuilder.Build(context, gameConfiguration, Number, SetName);

            AssertSet(gameConfiguration);
        }
    }

    /// <summary>
    /// Tests that the pieces are only linked to the set groups in the last step, so that a running game server
    /// gets the set groups before the links to them.
    /// </summary>
    [Test]
    public async Task LinksTheSetGroupsInTheLastStepAsync()
    {
        var (context, gameConfiguration) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);
        using (context)
        {
            var pieces = SkinArmorSetBuilder.AddMissingPieces(context, gameConfiguration, Number, SetName);
            var setGroups = SkinArmorSetBuilder.AddMissingSetGroups(context, gameConfiguration, pieces);

            Assert.That(setGroups, Has.Count.EqualTo(7));
            Assert.That(pieces.All(p => p.PossibleItemSetGroups.Count == 0), Is.True);
            Assert.That(SkinArmorSetBuilder.LinkSetGroups(pieces, setGroups), Is.True);
            Assert.That(SkinArmorSetBuilder.LinkSetGroups(pieces, setGroups), Is.False);
        }
    }

    private static void AssertSet(GameConfiguration gameConfiguration)
    {
        var pieces = gameConfiguration.Items.Where(i => i.Number == Number && ArmorGroups.Contains(i.Group)).OrderBy(i => i.Group).ToList();
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
            Assert.That(piece.DropsFromMonsters, Is.False, name);
            Assert.That(piece.PossibleItemOptions.Any(o => o.PossibleOptions.Any(p => p.OptionType == ItemOptionTypes.Excellent)), Is.True, name);

            // The defense rate bonus (any level) and the defense bonus of each set level from 10 to 15.
            Assert.That(piece.PossibleItemSetGroups, Has.Count.EqualTo(7), name);
            Assert.That(piece.PossibleItemSetGroups.All(g => g.MinimumItemCount == 5 && g.Items.Count == 5), Is.True, name);
            Assert.That(piece.PossibleItemSetGroups.All(g => g.Name.Value!.StartsWith("Pink ", StringComparison.Ordinal)), Is.True, name);
            Assert.That(piece.PossibleItemSetGroups.All(gameConfiguration.ItemSetGroups.Contains), Is.True, name);
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
