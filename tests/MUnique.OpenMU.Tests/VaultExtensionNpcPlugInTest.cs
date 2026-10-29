// <copyright file="VaultExtensionNpcPlugInTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.GameLogic.PlugIns.VaultExtension;
using MUnique.OpenMU.GameLogic.Views.Quest;
using MUnique.OpenMU.Pathfinding;
using Moq;

/// <summary>
/// Tests the <see cref="VaultExtensionNpcPlugIn"/>.
/// </summary>
[TestFixture]
public class VaultExtensionNpcPlugInTest
{
    private static readonly ItemDefinition PackedSoul = new() { Group = 12, Number = 31, Width = 1, Height = 1 };
    private static readonly ItemDefinition PackedLife = new() { Group = 12, Number = 136, Width = 1, Height = 1 };
    private static readonly ItemDefinition PackedChaos = new() { Group = 12, Number = 141, Width = 1, Height = 1 };

    /// <summary>
    /// Tests that accepting the dialog takes the packs of jewels and extends the vault.
    /// </summary>
    [Test]
    public async Task AcceptingExtendsTheVaultForThePriceAsync()
    {
        var (player, npc, plugIn, dialogIds) = await CreateAsync().ConfigureAwait(false);
        await AddItemAsync(player, 20, PackedSoul).ConfigureAwait(false);
        await AddItemAsync(player, 21, PackedLife).ConfigureAwait(false);
        await AddItemAsync(player, 22, PackedChaos).ConfigureAwait(false);
        await AddItemAsync(player, 23, PackedChaos).ConfigureAwait(false);

        var eventArgs = new NpcTalkEventArgs();
        await plugIn.PlayerTalksToNpcAsync(player, npc, eventArgs).ConfigureAwait(false);

        Assert.That(eventArgs.HasBeenHandled, Is.True);
        Assert.That(dialogIds, Has.Count.EqualTo(1));
        Assert.That(player.Account!.IsVaultExtended, Is.False, "nothing happens before accepting");

        Assert.That(await NpcDialogs.TryAnswerAsync(player, dialogIds[0], true).ConfigureAwait(false), Is.True);

        Assert.That(player.Account.IsVaultExtended, Is.True);
        Assert.That(player.Inventory!.Items.Select(i => i.Definition), Is.EqualTo(new[] { PackedChaos }), "only one pack of each is taken");
    }

    /// <summary>
    /// Tests that the vault isn't extended when an item of the price is missing, and nothing is taken.
    /// </summary>
    [Test]
    public async Task MissingItemKeepsEverythingAsync()
    {
        var (player, npc, plugIn, dialogIds) = await CreateAsync().ConfigureAwait(false);
        await AddItemAsync(player, 20, PackedSoul).ConfigureAwait(false);
        await AddItemAsync(player, 21, PackedLife).ConfigureAwait(false);

        await plugIn.PlayerTalksToNpcAsync(player, npc, new NpcTalkEventArgs()).ConfigureAwait(false);
        await NpcDialogs.TryAnswerAsync(player, dialogIds[0], true).ConfigureAwait(false);

        Assert.That(player.Account!.IsVaultExtended, Is.False);
        Assert.That(player.Inventory!.Items.Count(), Is.EqualTo(2));
    }

    /// <summary>
    /// Tests that declining, answering twice or answering far away from the NPC doesn't extend the vault.
    /// </summary>
    [Test]
    public async Task DecliningOrWalkingAwayDoesNothingAsync()
    {
        var (player, npc, plugIn, dialogIds) = await CreateAsync().ConfigureAwait(false);
        await AddItemAsync(player, 20, PackedSoul).ConfigureAwait(false);
        await AddItemAsync(player, 21, PackedLife).ConfigureAwait(false);
        await AddItemAsync(player, 22, PackedChaos).ConfigureAwait(false);

        await plugIn.PlayerTalksToNpcAsync(player, npc, new NpcTalkEventArgs()).ConfigureAwait(false);
        await NpcDialogs.TryAnswerAsync(player, dialogIds[0], false).ConfigureAwait(false);
        await NpcDialogs.TryAnswerAsync(player, dialogIds[0], true).ConfigureAwait(false);
        Assert.That(player.Account!.IsVaultExtended, Is.False);

        await plugIn.PlayerTalksToNpcAsync(player, npc, new NpcTalkEventArgs()).ConfigureAwait(false);
        npc.Position = new Point((byte)(player.Position.X + 30), player.Position.Y);
        await NpcDialogs.TryAnswerAsync(player, dialogIds[1], true).ConfigureAwait(false);
        Assert.That(player.Account.IsVaultExtended, Is.False);
        Assert.That(player.Inventory!.Items.Count(), Is.EqualTo(3));
    }

    /// <summary>
    /// Tests that a player whose vault is already extended gets no dialog, and that other NPCs are left to the other plugins.
    /// </summary>
    [Test]
    public async Task NoDialogWhenAlreadyExtendedOrOtherNpcAsync()
    {
        var (player, npc, plugIn, dialogIds) = await CreateAsync().ConfigureAwait(false);
        player.Account!.IsVaultExtended = true;
        await plugIn.PlayerTalksToNpcAsync(player, npc, new NpcTalkEventArgs()).ConfigureAwait(false);
        Assert.That(dialogIds, Is.Empty);

        var otherNpc = new NonPlayerCharacter(null!, new MonsterDefinition { Number = 240 }, player.CurrentMap!) { Position = player.Position };
        var eventArgs = new NpcTalkEventArgs();
        await plugIn.PlayerTalksToNpcAsync(player, otherNpc, eventArgs).ConfigureAwait(false);
        Assert.That(eventArgs.HasBeenHandled, Is.False);
    }

    /// <summary>
    /// Tests that the dialogs of the quests, whose ids count up from 1, are left to the quests.
    /// </summary>
    [Test]
    public async Task QuestDialogIdsAreNotHandledAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        Assert.That(await NpcDialogs.TryAnswerAsync(player, 1, true).ConfigureAwait(false), Is.False);
    }

    private static async ValueTask<(Player Player, NonPlayerCharacter Npc, VaultExtensionNpcPlugIn PlugIn, List<uint> DialogIds)> CreateAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        ClientFeatures.Set(player, ClientFeature.NpcQuestDialog);
        var definition = new MonsterDefinition { Number = VaultExtensionConfiguration.DefaultNpcNumber, Designation = "Guardián del Baúl" };
        var npc = new NonPlayerCharacter(null!, definition, player.CurrentMap!) { Position = player.Position };
        var plugIn = new VaultExtensionNpcPlugIn { Configuration = new VaultExtensionConfiguration() };
        var dialogIds = new List<uint>();
        Mock.Get(player.ViewPlugIns.GetPlugIn<INpcQuestDialogViewPlugIn>()!)
            .Setup(p => p.ShowNpcQuestDialogAsync(npc, It.IsAny<uint>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<NonPlayerCharacter, uint, string, string>((_, id, _, _) => dialogIds.Add(id))
            .Returns(ValueTask.CompletedTask);
        return (player, npc, plugIn, dialogIds);
    }

    private static async ValueTask AddItemAsync(Player player, byte slot, ItemDefinition definition)
    {
        await player.Inventory!.AddItemAsync(slot, new Item { Definition = definition, Durability = 1 }).ConfigureAwait(false);
    }
}
