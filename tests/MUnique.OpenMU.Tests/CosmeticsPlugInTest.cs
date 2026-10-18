// <copyright file="CosmeticsPlugInTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Moq;
using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.GameLogic.PlugIns.Cosmetics;
using MUnique.OpenMU.GameLogic.PlugIns.WeeklyQuests;
using MUnique.OpenMU.Persistence.Progression;

/// <summary>
/// Tests for the <see cref="CosmeticsPlugIn"/>.
/// </summary>
[TestFixture]
public class CosmeticsPlugInTest
{
    private const short DragonHelm = 11;
    private const short DragonArmor = 12;
    private const short BlackDragonHelm = 21;
    private const short DragonItemNumber = 1;
    private const short BlackDragonItemNumber = 16;
    private const byte PieceLevel = 11;

    /// <summary>
    /// Tests that a worn piece replaces only the item of its slot.
    /// </summary>
    [Test]
    public async Task WornPieceReplacesItsSlotAsync()
    {
        var player = await CreatePlayerAsync().ConfigureAwait(false);
        var plugIn = CreatePlugIn(new InMemoryProgressionRepository());
        await plugIn.GrantAsync(player, DragonHelm, "test").ConfigureAwait(false);

        Assert.That(await plugIn.WearAsync(player, DragonHelm).ConfigureAwait(false), Is.EqualTo(CosmeticChangeResult.Worn));

        var item = player.AppearanceData.EquippedItems.Single();
        Assert.That(item.ItemSlot, Is.EqualTo(InventoryConstants.HelmSlot));
        Assert.That(item.Definition!.Group, Is.EqualTo(7));
        Assert.That(item.Definition.Number, Is.EqualTo(DragonItemNumber));
        Assert.That(item.Level, Is.EqualTo(PieceLevel));
    }

    /// <summary>
    /// Tests that pieces of different sets can be combined, and that a piece replaces the one which was worn in its slot.
    /// </summary>
    [Test]
    public async Task PiecesOfDifferentSetsCanBeCombinedAsync()
    {
        var player = await CreatePlayerAsync().ConfigureAwait(false);
        var plugIn = CreatePlugIn(new InMemoryProgressionRepository());
        foreach (var piece in new[] { DragonHelm, DragonArmor, BlackDragonHelm })
        {
            await plugIn.GrantAsync(player, piece, "test").ConfigureAwait(false);
            await plugIn.WearAsync(player, piece).ConfigureAwait(false);
        }

        var items = player.AppearanceData.EquippedItems.ToDictionary(i => i.ItemSlot, i => i.Definition!.Number);
        Assert.That(items, Has.Count.EqualTo(2));
        Assert.That(items[InventoryConstants.HelmSlot], Is.EqualTo(BlackDragonItemNumber));
        Assert.That(items[InventoryConstants.ArmorSlot], Is.EqualTo(DragonItemNumber));
    }

    /// <summary>
    /// Tests that one piece or all of them can be taken off.
    /// </summary>
    [Test]
    public async Task PiecesCanBeTakenOffAsync()
    {
        var player = await CreatePlayerAsync().ConfigureAwait(false);
        var plugIn = CreatePlugIn(new InMemoryProgressionRepository());
        foreach (var piece in new[] { DragonHelm, DragonArmor })
        {
            await plugIn.GrantAsync(player, piece, "test").ConfigureAwait(false);
            await plugIn.WearAsync(player, piece).ConfigureAwait(false);
        }

        Assert.That(await plugIn.TakeOffAsync(player, DragonHelm).ConfigureAwait(false), Is.EqualTo(CosmeticChangeResult.TakenOff));
        Assert.That(player.AppearanceData.EquippedItems.Select(i => i.ItemSlot), Is.EquivalentTo(new[] { InventoryConstants.ArmorSlot }));

        Assert.That(await plugIn.TakeOffAsync(player, null).ConfigureAwait(false), Is.EqualTo(CosmeticChangeResult.TakenOff));
        Assert.That(PlayerCosmetics.IsWearingAny(player), Is.False);
        Assert.That(await plugIn.TakeOffAsync(player, null).ConfigureAwait(false), Is.EqualTo(CosmeticChangeResult.NotWorn));
    }

    /// <summary>
    /// Tests that a piece can't be worn before it has been unlocked, and that unknown pieces are refused.
    /// </summary>
    [Test]
    public async Task LockedPieceCannotBeWornAsync()
    {
        var player = await CreatePlayerAsync().ConfigureAwait(false);
        var plugIn = CreatePlugIn(new InMemoryProgressionRepository());

        Assert.That(await plugIn.WearAsync(player, DragonHelm).ConfigureAwait(false), Is.EqualTo(CosmeticChangeResult.NotUnlocked));
        Assert.That(await plugIn.WearAsync(player, 999).ConfigureAwait(false), Is.EqualTo(CosmeticChangeResult.Unknown));
        Assert.That(PlayerCosmetics.IsWearingAny(player), Is.False);
    }

    /// <summary>
    /// Tests that a piece of another class can't be worn, unless the configuration allows it.
    /// </summary>
    [Test]
    public async Task PieceOfAnotherClassCannotBeWornAsync()
    {
        var player = await CreatePlayerAsync(qualified: false).ConfigureAwait(false);
        var plugIn = CreatePlugIn(new InMemoryProgressionRepository());
        await plugIn.GrantAsync(player, DragonHelm, "test").ConfigureAwait(false);

        Assert.That(await plugIn.WearAsync(player, DragonHelm).ConfigureAwait(false), Is.EqualTo(CosmeticChangeResult.NotQualified));

        plugIn.Configuration!.RestrictToQualifiedClasses = false;
        Assert.That(await plugIn.WearAsync(player, DragonHelm).ConfigureAwait(false), Is.EqualTo(CosmeticChangeResult.Worn));
    }

    /// <summary>
    /// Tests that removing a worn piece takes it off.
    /// </summary>
    [Test]
    public async Task RevokingAWornPieceTakesItOffAsync()
    {
        var player = await CreatePlayerAsync().ConfigureAwait(false);
        var plugIn = CreatePlugIn(new InMemoryProgressionRepository());
        await plugIn.GrantAsync(player, DragonHelm, "test").ConfigureAwait(false);
        await plugIn.WearAsync(player, DragonHelm).ConfigureAwait(false);

        Assert.That(await plugIn.RevokeAsync(player, DragonHelm).ConfigureAwait(false), Is.True);

        Assert.That(PlayerCosmetics.IsWearingAny(player), Is.False);
        var helm = (await plugIn.GetPiecesAsync(player).ConfigureAwait(false))!.Single(p => p.Definition.Number == DragonHelm);
        Assert.That(helm.IsUnlocked, Is.False);
        Assert.That(helm.IsWorn, Is.False);
    }

    /// <summary>
    /// Tests that the worn pieces are stored, so that the character wears them again when it enters the game the next time.
    /// </summary>
    [Test]
    public async Task WornPiecesAreRestoredWhenEnteringTheGameAsync()
    {
        var repository = new InMemoryProgressionRepository();
        var player = await CreatePlayerAsync().ConfigureAwait(false);
        var plugIn = CreatePlugIn(repository);
        foreach (var piece in new[] { BlackDragonHelm, DragonArmor })
        {
            await plugIn.GrantAsync(player, piece, "test").ConfigureAwait(false);
            await plugIn.WearAsync(player, piece).ConfigureAwait(false);
        }

        await plugIn.PlayerStateChangedAsync(player, PlayerState.EnteredWorld, PlayerState.CharacterSelection).ConfigureAwait(false);
        Assert.That(PlayerCosmetics.IsWearingAny(player), Is.False);

        var restartedPlugIn = CreatePlugIn(repository);
        await restartedPlugIn.PlayerStateChangedAsync(player, PlayerState.CharacterSelection, PlayerState.EnteredWorld).ConfigureAwait(false);

        Assert.That(PlayerCosmetics.GetPiece(player, InventoryConstants.HelmSlot)?.Definition?.Number, Is.EqualTo(BlackDragonItemNumber));
        Assert.That(PlayerCosmetics.GetPiece(player, InventoryConstants.ArmorSlot)?.Definition?.Number, Is.EqualTo(DragonItemNumber));
    }

    /// <summary>
    /// Tests that a piece can be given as reward of a quest, an achievement, a prestige level or the season pass.
    /// </summary>
    [Test]
    public async Task PieceIsGivenAsRewardAsync()
    {
        var player = await CreatePlayerAsync().ConfigureAwait(false);
        var plugIn = CreatePlugIn(new InMemoryProgressionRepository());
        player.GameContext.PlugInManager.RegisterPlugInAtPlugInPoint<IPlayerStateChangedPlugIn>(plugIn);
        var rewards = new List<WeeklyQuestReward> { new() { RewardType = WeeklyQuestRewardType.CosmeticPiece, CosmeticPieceNumber = DragonArmor } };

        Assert.That(await WeeklyQuestRewarder.TryGiveRewardsAsync(player, rewards, "quest").ConfigureAwait(false), Is.True);

        var pieces = (await plugIn.GetPiecesAsync(player).ConfigureAwait(false))!;
        Assert.That(pieces.Where(p => p.IsUnlocked).Select(p => p.Definition.Number), Is.EquivalentTo(new[] { DragonArmor }));
    }

    /// <summary>
    /// Tests that the default configuration has the five pieces of each of its sets, with unique numbers.
    /// </summary>
    [Test]
    public void DefaultConfigurationHasAllPiecesOfItsSets()
    {
        var pieces = CosmeticsConfiguration.Default.Pieces;

        Assert.That(pieces.Select(p => p.Number), Is.Unique);
        Assert.That(pieces.GroupBy(p => p.ItemNumber).Select(g => g.Count()), Is.All.EqualTo(5));
        Assert.That(pieces.Single(p => p.Number == DragonHelm).Slot, Is.EqualTo(CosmeticSlot.Helm));
        Assert.That(pieces.Single(p => p.Number == DragonArmor).Slot, Is.EqualTo(CosmeticSlot.Armor));
    }

    private static CosmeticsPlugIn CreatePlugIn(IProgressionRepository repository)
    {
        return new CosmeticsPlugIn(repository)
        {
            Configuration = new CosmeticsConfiguration
            {
                Pieces = new List<CosmeticPieceDefinition>
                {
                    new() { Number = DragonHelm, Slot = CosmeticSlot.Helm, ItemNumber = DragonItemNumber, Level = PieceLevel },
                    new() { Number = DragonArmor, Slot = CosmeticSlot.Armor, ItemNumber = DragonItemNumber, Level = PieceLevel },
                    new() { Number = BlackDragonHelm, Slot = CosmeticSlot.Helm, ItemNumber = BlackDragonItemNumber, Level = PieceLevel },
                },
            },
        };
    }

    private static async ValueTask<Player> CreatePlayerAsync(bool qualified = true)
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.GameContext.Configuration.MaximumInventoryMoney = int.MaxValue;
        foreach (var (group, number) in new (byte, short)[] { (7, DragonItemNumber), (8, DragonItemNumber), (7, BlackDragonItemNumber) })
        {
            var definition = new Mock<ItemDefinition>();
            definition.SetupAllProperties();
            definition.Setup(d => d.QualifiedCharacters).Returns(new List<CharacterClass>());
            definition.Object.Group = group;
            definition.Object.Number = number;
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
