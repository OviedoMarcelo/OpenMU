// <copyright file="CosmeticSetsChatCommandPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlugIns.ChatCommands.Arguments;
using MUnique.OpenMU.GameLogic.PlugIns.CosmeticSets;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// A chat command plugin which shows the unlocked cosmetic sets of the character.
/// </summary>
[Guid("A3E95B17-6C2D-4F48-B0D1-94E7A2C6F538")]
[PlugIn]
[Display(Name = nameof(PlugInResources.CosmeticSetsChatCommandPlugIn_Name), Description = nameof(PlugInResources.CosmeticSetsChatCommandPlugIn_Description), ResourceType = typeof(PlugInResources))]
[ChatCommandHelp(Command, typeof(EmptyChatCommandArgs), CharacterStatus.Normal)]
public class CosmeticSetsChatCommandPlugIn : ChatCommandPlugInBase<EmptyChatCommandArgs>
{
    private const string Command = "/cosmeticos";

    /// <inheritdoc />
    public override string Key => Command;

    /// <inheritdoc />
    public override CharacterStatus MinCharacterStatusRequirement => CharacterStatus.Normal;

    /// <inheritdoc />
    protected override async ValueTask DoHandleCommandAsync(Player player, EmptyChatCommandArgs arguments)
    {
        if (CosmeticSetsPlugIn.GetActivePlugIn(player) is not { } plugIn
            || await plugIn.GetSetsAsync(player).ConfigureAwait(false) is not { } sets)
        {
            await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.CosmeticSetsNotAvailable)).ConfigureAwait(false);
            return;
        }

        var unlocked = sets.Sets.Where(s => s.IsUnlocked).ToList();
        if (unlocked.Count == 0)
        {
            await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.CosmeticSetListEmpty)).ConfigureAwait(false);
            return;
        }

        await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.CosmeticSetListHeader)).ConfigureAwait(false);
        foreach (var entry in unlocked)
        {
            var messageKey = entry.Definition == sets.Active ? nameof(PlayerMessage.CosmeticSetListEntryWorn) : nameof(PlayerMessage.CosmeticSetListEntry);
            await player.ShowLocalizedBlueMessageAsync(messageKey, entry.Definition.Number, entry.Definition.Name).ConfigureAwait(false);
        }
    }
}
