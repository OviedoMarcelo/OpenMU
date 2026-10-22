// <copyright file="SkinArmorSetDataTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.Persistence.Initialization.CharacterClasses;
using MUnique.OpenMU.Persistence.Initialization.Items;
using MUnique.OpenMU.Persistence.InMemory;

/// <summary>
/// Tests the <see cref="SkinArmorSetBuilder"/>, which builds the armor sets of skin packs as skins without stats.
/// </summary>
[TestFixture]
internal class SkinArmorSetDataTest
{
    private const short Number = 55;

    private const string SetName = "Toranaga";

    private static readonly byte[] ArmorGroups = [7, 8, 9, 10, 11];

    /// <summary>
    /// Tests that the builder builds the whole set, as skins without stats.
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

            SkinArmorSetBuilder.Build(context, gameConfiguration, Number, SetName);

            Assert.That(gameConfiguration.Items, Has.Count.EqualTo(itemCount));
            AssertSet(gameConfiguration);
        }
    }

    /// <summary>
    /// Tests that building a set whose pieces are a real armor takes their stats off and that its set groups can be removed,
    /// like for the sets which were added as armors before.
    /// </summary>
    [Test]
    public async Task TakesTheStatsOffARealArmorAsync()
    {
        var (context, gameConfiguration) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);
        using (context)
        {
            var pieces = SkinArmorSetBuilder.Build(context, gameConfiguration, Number, SetName);
            var greatDragonHelm = SkinArmorSetBuilder.FindItem(gameConfiguration, 7, 21)!;
            var setGroup = context.CreateNew<ItemSetGroup>();
            foreach (var piece in pieces)
            {
                piece.DropsFromMonsters = true;
                piece.PossibleItemOptions.Add(greatDragonHelm.PossibleItemOptions.First());
                piece.BasePowerUpAttributes.Add(context.CreateNew<ItemBasePowerUpDefinition>());
                piece.PossibleItemSetGroups.Add(setGroup);
                var itemOfSet = context.CreateNew<ItemOfItemSet>();
                itemOfSet.ItemDefinition = piece;
                setGroup.Items.Add(itemOfSet);
            }

            gameConfiguration.ItemSetGroups.Add(setGroup);
            Assert.That(pieces.Any(SkinArmorSetBuilder.IsSkinOnly), Is.False);

            SkinArmorSetBuilder.Build(context, gameConfiguration, Number, SetName);
            AssertSet(gameConfiguration);

            Assert.That(SkinArmorSetBuilder.RemoveSetGroups(gameConfiguration, Number), Is.EqualTo(new[] { setGroup }));
            Assert.That(gameConfiguration.ItemSetGroups, Does.Not.Contain(setGroup));
            Assert.That(SkinArmorSetBuilder.MakeSkinOnly(pieces[0]), Is.False);
        }
    }

    /// <summary>
    /// Tests that the skins of wings and pets are single items without stats, with the size and slot of their template,
    /// which every class can wear.
    /// </summary>
    [Test]
    public async Task BuildsWingsAndPetSkinsAsync()
    {
        var (context, gameConfiguration) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);
        using (context)
        {
            var angel = SkinArmorSetBuilder.FindItem(gameConfiguration, SkinItemBuilder.PetGroup, 0)!;
            var wingsOfElf = SkinArmorSetBuilder.FindItem(gameConfiguration, SkinItemBuilder.WingsGroup, SkinItemBuilder.WingsTemplateNumber)!;

            var pet = SkinItemBuilder.Build(context, gameConfiguration, SkinItemBuilder.PetGroup, 200, "Archangel", angel);
            var wings = SkinItemBuilder.Build(context, gameConfiguration, SkinItemBuilder.WingsGroup, 200, "Blue Wings", wingsOfElf);
            var itemCount = gameConfiguration.Items.Count;

            foreach (var (skin, template) in new[] { (pet, angel), (wings, wingsOfElf) })
            {
                var name = skin.Name.Value;
                Assert.That(SkinItemBuilder.Build(context, gameConfiguration, skin.Group, skin.Number, name!, template), Is.SameAs(skin), name);
                Assert.That(SkinArmorSetBuilder.IsSkinOnly(skin), Is.True, name);
                Assert.That(skin.ItemSlot, Is.SameAs(template.ItemSlot), name);
                Assert.That((skin.Width, skin.Height), Is.EqualTo((template.Width, template.Height)), name);
                Assert.That(skin.QualifiedCharacters, Has.Count.EqualTo(gameConfiguration.CharacterClasses.Count), name);
            }

            Assert.That(gameConfiguration.Items, Has.Count.EqualTo(itemCount));
        }
    }

    private static void AssertSet(GameConfiguration gameConfiguration)
    {
        var pieces = gameConfiguration.Items.Where(i => i.Number == Number && ArmorGroups.Contains(i.Group)).OrderBy(i => i.Group).ToList();
        Assert.That(pieces.Select(p => p.Group), Is.EqualTo(ArmorGroups));
        Assert.That(pieces.Select(p => p.Name.Value), Is.EqualTo(new[] { "Toranaga Helm", "Toranaga Armor", "Toranaga Pants", "Toranaga Gloves", "Toranaga Boots" }));

        var allClasses = gameConfiguration.CharacterClasses.Select(c => c.Number).Order().ToList();
        var rageFighters = new[] { (byte)CharacterClassNumber.RageFighter, (byte)CharacterClassNumber.FistMaster };
        foreach (var piece in pieces)
        {
            var name = piece.Name.Value;

            // Every class, except the rage fighter for the gloves.
            var expectedClasses = piece.Group == 10 ? allClasses.Except(rageFighters).ToList() : allClasses;
            Assert.That(piece.QualifiedCharacters.Select(c => c.Number).Order(), Is.EqualTo(expectedClasses), name);
            Assert.That(piece.Requirements, Is.Empty, name);

            // Only a skin: no stats, and it doesn't drop.
            Assert.That(piece.DropsFromMonsters, Is.False, name);
            Assert.That(piece.BasePowerUpAttributes, Is.Empty, name);
            Assert.That(piece.PossibleItemOptions, Is.Empty, name);
            Assert.That(piece.PossibleItemSetGroups, Is.Empty, name);
            Assert.That(SkinArmorSetBuilder.IsSkinOnly(piece), Is.True, name);
        }
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
