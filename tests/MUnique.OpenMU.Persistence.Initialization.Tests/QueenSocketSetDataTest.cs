// <copyright file="QueenSocketSetDataTest.cs" company="MUnique">
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
/// Tests the Queen set, the socket armor set of the Dimension Master.
/// </summary>
[TestFixture]
internal class QueenSocketSetDataTest
{
    private const short QueenNumber = 53;

    private static readonly byte[] ArmorGroups = [7, 8, 9, 10, 11];

    /// <summary>
    /// Tests that a new season 6 database contains the Queen set.
    /// </summary>
    [Test]
    public async Task NewDatabaseContainsQueenSetAsync()
    {
        var (_, gameConfiguration) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);

        AssertQueenSet(gameConfiguration);
    }

    /// <summary>
    /// Tests that the update adds the same Queen set to a database which was created before it existed.
    /// </summary>
    [Test]
    public async Task UpdateAddsQueenSetToExistingDatabaseAsync()
    {
        var (context, gameConfiguration) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);
        using (context)
        {
            var queenItems = gameConfiguration.Items.Where(IsQueenPiece).ToList();
            foreach (var setGroup in gameConfiguration.ItemSetGroups.Where(g => g.Items.Any(i => i.ItemDefinition is { } item && IsQueenPiece(item))).ToList())
            {
                gameConfiguration.ItemSetGroups.Remove(setGroup);
            }

            foreach (var item in queenItems)
            {
                gameConfiguration.Items.Remove(item);
            }

            Assert.That(gameConfiguration.Items.Any(IsQueenPiece), Is.False);

            await new AddQueenSocketSetPlugIn().ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);

            AssertQueenSet(gameConfiguration);
        }
    }

    private static void AssertQueenSet(GameConfiguration gameConfiguration)
    {
        var pieces = gameConfiguration.Items.Where(IsQueenPiece).OrderBy(i => i.Group).ToList();
        Assert.That(pieces.Select(p => p.Group), Is.EqualTo(ArmorGroups));
        Assert.That(pieces.Select(p => p.Name.Value), Is.EqualTo(new[] { "Queen Helm", "Queen Armor", "Queen Pants", "Queen Gloves", "Queen Boots" }));
        Assert.That(pieces.Select(p => Defense(p)), Is.EqualTo(new[] { 44f, 60f, 50f, 39f, 42f }));

        foreach (var piece in pieces)
        {
            var name = piece.Name.Value;
            Assert.That(piece.MaximumSockets, Is.EqualTo(3), name);
            Assert.That(piece.QualifiedCharacters.Select(c => c.Number), Is.EqualTo(new[] { (byte)CharacterClassNumber.DimensionMaster }), name);
            Assert.That(Requirement(piece, Stats.Level), Is.EqualTo(400), name);
            Assert.That(Requirement(piece, Stats.TotalStrengthRequirementValue), Is.EqualTo(175), name);
            Assert.That(Requirement(piece, Stats.TotalAgilityRequirementValue), Is.EqualTo(85), name);
            Assert.That(piece.PossibleItemOptions.Any(o => o.PossibleOptions.Any(p => p.OptionType == ItemOptionTypes.SocketOption)), Is.True, name);
            Assert.That(piece.PossibleItemOptions.Any(o => o.PossibleOptions.Any(p => p.OptionType == ItemOptionTypes.Excellent)), Is.True, name);

            // The defense rate bonus (any level) and the defense bonus of each set level from 10 to 15.
            Assert.That(piece.PossibleItemSetGroups, Has.Count.EqualTo(7), name);
            Assert.That(piece.PossibleItemSetGroups.All(g => g.MinimumItemCount == 5 && g.Items.Count == 5), Is.True, name);
        }
    }

    private static bool IsQueenPiece(ItemDefinition item) => item.Number == QueenNumber && ArmorGroups.Contains(item.Group);

    private static float Defense(ItemDefinition item) => item.BasePowerUpAttributes.Single(p => p.TargetAttribute?.Id == Stats.DefenseBase.Id).BaseValue;

    private static int Requirement(ItemDefinition item, AttributeSystem.AttributeDefinition attribute) =>
        item.Requirements.Single(r => r.Attribute?.Id == attribute.Id).MinimumValue;

    private static async Task<(IContext Context, GameConfiguration GameConfiguration)> CreateSeason6ConfigurationAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        var dataInitialization = new VersionSeasonSix.DataInitialization(contextProvider, new NullLoggerFactory());
        await dataInitialization.CreateInitialDataAsync(1, true).ConfigureAwait(false);

        var context = contextProvider.CreateNewContext();
        return (context, (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First());
    }
}
