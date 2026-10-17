// <copyright file="CosmeticSetsPlugInTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Moq;
using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.GameLogic.PlugIns.CosmeticSets;
using MUnique.OpenMU.GameLogic.PlugIns.WeeklyQuests;
using MUnique.OpenMU.Persistence.Progression;

/// <summary>
/// Tests for the <see cref="CosmeticSetsPlugIn"/>.
/// </summary>
[TestFixture]
public class CosmeticSetsPlugInTest
{
    private const short SetNumber = 1;
    private const short ItemNumber = 16;
    private const byte SetLevel = 11;
    private const byte HelmGroup = 7;
    private const byte BootsGroup = 11;

    /// <summary>
    /// Tests that a worn set replaces the visible armor pieces, without touching the other slots.
    /// </summary>
    [Test]
    public async Task WornSetReplacesTheArmorAsync()
    {
        var player = await CreatePlayerAsync().ConfigureAwait(false);
        var plugIn = CreatePlugIn(new InMemoryProgressionRepository());

        Assert.That(await plugIn.GrantAsync(player, SetNumber, "test").ConfigureAwait(false), Is.Not.Null);
        Assert.That(await plugIn.SetActiveSetAsync(player, SetNumber).ConfigureAwait(false), Is.EqualTo(CosmeticSetChangeResult.Changed));

        var items = player.AppearanceData.EquippedItems.ToList();
        Assert.That(items.Select(i => i.ItemSlot), Is.EquivalentTo(new[] { InventoryConstants.HelmSlot, InventoryConstants.ArmorSlot, InventoryConstants.PantsSlot, InventoryConstants.GlovesSlot, InventoryConstants.BootsSlot }));
        Assert.That(items.Select(i => i.Definition!.Number), Is.All.EqualTo(ItemNumber));
        Assert.That(items.Select(i => i.Definition!.Group), Is.EquivalentTo(new byte[] { 7, 8, 9, 10, 11 }));
        Assert.That(items.Select(i => i.Level), Is.All.EqualTo(SetLevel));
        Assert.That(items.Single(i => i.ItemSlot == InventoryConstants.ArmorSlot).Definition!.Group, Is.EqualTo(8));
    }

    /// <summary>
    /// Tests that the real armor is visible again when the set is taken off.
    /// </summary>
    [Test]
    public async Task TakingOffTheSetShowsTheArmorAgainAsync()
    {
        var player = await CreatePlayerAsync().ConfigureAwait(false);
        var plugIn = CreatePlugIn(new InMemoryProgressionRepository());
        await plugIn.GrantAsync(player, SetNumber, "test").ConfigureAwait(false);
        await plugIn.SetActiveSetAsync(player, SetNumber).ConfigureAwait(false);

        Assert.That(await plugIn.SetActiveSetAsync(player, null).ConfigureAwait(false), Is.EqualTo(CosmeticSetChangeResult.Removed));

        Assert.That(player.AppearanceData.EquippedItems, Is.Empty);
        Assert.That(PlayerCosmetics.IsWearingSet(player), Is.False);
    }

    /// <summary>
    /// Tests that a set can't be worn before it has been unlocked.
    /// </summary>
    [Test]
    public async Task LockedSetCannotBeWornAsync()
    {
        var player = await CreatePlayerAsync().ConfigureAwait(false);
        var plugIn = CreatePlugIn(new InMemoryProgressionRepository());

        Assert.That(await plugIn.SetActiveSetAsync(player, SetNumber).ConfigureAwait(false), Is.EqualTo(CosmeticSetChangeResult.NotUnlocked));
        Assert.That(await plugIn.SetActiveSetAsync(player, 99).ConfigureAwait(false), Is.EqualTo(CosmeticSetChangeResult.Unknown));
        Assert.That(PlayerCosmetics.IsWearingSet(player), Is.False);
    }

    /// <summary>
    /// Tests that a set of another class can't be worn, unless the configuration allows it.
    /// </summary>
    [Test]
    public async Task SetOfAnotherClassCannotBeWornAsync()
    {
        var player = await CreatePlayerAsync(qualified: false).ConfigureAwait(false);
        var plugIn = CreatePlugIn(new InMemoryProgressionRepository());
        await plugIn.GrantAsync(player, SetNumber, "test").ConfigureAwait(false);

        Assert.That(await plugIn.SetActiveSetAsync(player, SetNumber).ConfigureAwait(false), Is.EqualTo(CosmeticSetChangeResult.NotQualified));

        plugIn.Configuration!.RestrictToQualifiedClasses = false;
        Assert.That(await plugIn.SetActiveSetAsync(player, SetNumber).ConfigureAwait(false), Is.EqualTo(CosmeticSetChangeResult.Changed));
    }

    /// <summary>
    /// Tests that removing the worn set takes it off.
    /// </summary>
    [Test]
    public async Task RevokingTheWornSetTakesItOffAsync()
    {
        var player = await CreatePlayerAsync().ConfigureAwait(false);
        var plugIn = CreatePlugIn(new InMemoryProgressionRepository());
        await plugIn.GrantAsync(player, SetNumber, "test").ConfigureAwait(false);
        await plugIn.SetActiveSetAsync(player, SetNumber).ConfigureAwait(false);

        Assert.That(await plugIn.RevokeAsync(player, SetNumber).ConfigureAwait(false), Is.True);

        Assert.That(PlayerCosmetics.IsWearingSet(player), Is.False);
        var sets = await plugIn.GetSetsAsync(player).ConfigureAwait(false);
        Assert.That(sets!.Value.Active, Is.Null);
        Assert.That(sets.Value.Sets.Single().IsUnlocked, Is.False);
    }

    /// <summary>
    /// Tests that the worn set is stored, so that the character wears it again when it enters the game the next time.
    /// </summary>
    [Test]
    public async Task WornSetIsRestoredWhenEnteringTheGameAsync()
    {
        var repository = new InMemoryProgressionRepository();
        var player = await CreatePlayerAsync().ConfigureAwait(false);
        var plugIn = CreatePlugIn(repository);
        await plugIn.GrantAsync(player, SetNumber, "test").ConfigureAwait(false);
        await plugIn.SetActiveSetAsync(player, SetNumber).ConfigureAwait(false);

        await plugIn.PlayerStateChangedAsync(player, PlayerState.EnteredWorld, PlayerState.CharacterSelection).ConfigureAwait(false);
        Assert.That(PlayerCosmetics.IsWearingSet(player), Is.False);

        var restartedPlugIn = CreatePlugIn(repository);
        await restartedPlugIn.PlayerStateChangedAsync(player, PlayerState.CharacterSelection, PlayerState.EnteredWorld).ConfigureAwait(false);

        Assert.That(PlayerCosmetics.IsWearingSet(player), Is.True);
        Assert.That(PlayerCosmetics.GetPiece(player, InventoryConstants.HelmSlot)?.Definition?.Number, Is.EqualTo(ItemNumber));
    }

    /// <summary>
    /// Tests that a set can be given as reward of a quest, an achievement, a prestige level or the season pass.
    /// </summary>
    [Test]
    public async Task SetIsGivenAsRewardAsync()
    {
        var player = await CreatePlayerAsync().ConfigureAwait(false);
        var plugIn = CreatePlugIn(new InMemoryProgressionRepository());
        player.GameContext.PlugInManager.RegisterPlugInAtPlugInPoint<IPlayerStateChangedPlugIn>(plugIn);
        var rewards = new List<WeeklyQuestReward> { new() { RewardType = WeeklyQuestRewardType.CosmeticSet, CosmeticSetNumber = SetNumber } };

        Assert.That(await WeeklyQuestRewarder.TryGiveRewardsAsync(player, rewards, "quest").ConfigureAwait(false), Is.True);

        var sets = await plugIn.GetSetsAsync(player).ConfigureAwait(false);
        Assert.That(sets!.Value.Sets.Single().IsUnlocked, Is.True);
    }

    private static CosmeticSetsPlugIn CreatePlugIn(IProgressionRepository repository)
    {
        return new CosmeticSetsPlugIn(repository)
        {
            Configuration = new CosmeticSetsConfiguration
            {
                Sets = new List<CosmeticSetDefinition>
                {
                    new() { Number = SetNumber, Name = "Black Dragon", ItemNumber = ItemNumber, Level = SetLevel },
                },
            },
        };
    }

    private static async ValueTask<Player> CreatePlayerAsync(bool qualified = true)
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.GameContext.Configuration.MaximumInventoryMoney = int.MaxValue;
        for (var group = HelmGroup; group <= BootsGroup; group++)
        {
            var definition = new Mock<ItemDefinition>();
            definition.SetupAllProperties();
            definition.Setup(d => d.QualifiedCharacters).Returns(new List<CharacterClass>());
            definition.Object.Group = group;
            definition.Object.Number = ItemNumber;
            if (qualified)
            {
                definition.Object.QualifiedCharacters.Add(player.SelectedCharacter!.CharacterClass!);
            }

            player.GameContext.Configuration.Items.Add(definition.Object);
        }

        await player.PlayerState.TryAdvanceToAsync(PlayerState.EnteredWorld).ConfigureAwait(false);
        return player;
    }
}
