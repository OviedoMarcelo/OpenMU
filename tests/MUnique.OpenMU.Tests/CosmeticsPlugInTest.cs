// <copyright file="CosmeticsPlugInTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Moq;
using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
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
    private const short ArchangelPiece = 13200;
    private const short BlueWingsPiece = 12200;
    private const short SkinItemNumber = 200;
    private const short GuardianAngelNumber = 0;
    private const short ImpNumber = 1;

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

    /// <summary>
    /// Tests that a pet skin is only shown while the player has the pet it's a skin of, and that wings are shown
    /// without wings, like the armor.
    /// </summary>
    [Test]
    public async Task PetSkinIsOnlyShownOverItsPetAsync()
    {
        var player = await CreatePlayerAsync().ConfigureAwait(false);
        var plugIn = CreatePlugIn(new InMemoryProgressionRepository());
        await plugIn.GrantAsync(player, ArchangelPiece, "test").ConfigureAwait(false);
        await plugIn.GrantAsync(player, BlueWingsPiece, "test").ConfigureAwait(false);

        Assert.That(await plugIn.WearAsync(player, ArchangelPiece).ConfigureAwait(false), Is.EqualTo(CosmeticChangeResult.Worn));
        Assert.That(await plugIn.WearAsync(player, BlueWingsPiece).ConfigureAwait(false), Is.EqualTo(CosmeticChangeResult.Worn));

        Assert.That(PlayerCosmetics.GetPiece(player, InventoryConstants.WingsSlot)?.Definition?.Number, Is.EqualTo(SkinItemNumber));
        Assert.That(PlayerCosmetics.GetPiece(player, InventoryConstants.PetSlot), Is.Null, "without a pet");

        var imp = await EquipPetAsync(player, ImpNumber).ConfigureAwait(false);
        Assert.That(PlayerCosmetics.GetPiece(player, InventoryConstants.PetSlot), Is.Null, "over another pet");

        await player.Inventory!.RemoveItemAsync(imp).ConfigureAwait(false);
        await EquipPetAsync(player, GuardianAngelNumber).ConfigureAwait(false);
        Assert.That(PlayerCosmetics.GetPiece(player, InventoryConstants.PetSlot)?.Definition?.Number, Is.EqualTo(SkinItemNumber));

        var pet = player.AppearanceData.EquippedItems.Single(i => i.ItemSlot == InventoryConstants.PetSlot);
        Assert.That(pet.Definition!.Group, Is.EqualTo(13));
        Assert.That(pet.Definition.Number, Is.EqualTo(SkinItemNumber));
    }

    private static async ValueTask<Item> EquipPetAsync(Player player, short number)
    {
        var item = new Mock<Item>();
        item.SetupAllProperties();
        item.Setup(i => i.ItemOptions).Returns(new List<ItemOptionLink>());
        item.Setup(i => i.ItemSetGroups).Returns(new List<ItemOfItemSet>());
        item.Object.Definition = player.GameContext.Configuration.Items.Single(i => i.Group == 13 && i.Number == number);
        await player.Inventory!.AddItemAsync(InventoryConstants.PetSlot, item.Object).ConfigureAwait(false);
        return item.Object;
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
                    new() { Number = ArchangelPiece, Slot = CosmeticSlot.Pet, ItemNumber = SkinItemNumber, OnlyOverItemNumber = GuardianAngelNumber },
                    new() { Number = BlueWingsPiece, Slot = CosmeticSlot.Wings, ItemNumber = SkinItemNumber },
                },
            },
        };
    }

    private static async ValueTask<Player> CreatePlayerAsync(bool qualified = true)
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.GameContext.Configuration.MaximumInventoryMoney = int.MaxValue;
        var items = new (byte, short)[]
        {
            (7, DragonItemNumber), (8, DragonItemNumber), (7, BlackDragonItemNumber),
            (12, SkinItemNumber), (13, SkinItemNumber), (13, GuardianAngelNumber), (13, ImpNumber),
        };
        foreach (var (group, number) in items)
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
