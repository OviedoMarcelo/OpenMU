// <copyright file="CosmeticsChatCommandPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlugIns.ChatCommands.Arguments;
using MUnique.OpenMU.GameLogic.PlugIns.Cosmetics;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// A chat command plugin which shows the unlocked cosmetic pieces of the character.
/// </summary>
[Guid("A3E95B17-6C2D-4F48-B0D1-94E7A2C6F538")]
[PlugIn]
[Display(Name = nameof(PlugInResources.CosmeticsChatCommandPlugIn_Name), Description = nameof(PlugInResources.CosmeticsChatCommandPlugIn_Description), ResourceType = typeof(PlugInResources))]
[ChatCommandHelp(Command, typeof(EmptyChatCommandArgs), CharacterStatus.Normal)]
public class CosmeticsChatCommandPlugIn : ChatCommandPlugInBase<EmptyChatCommandArgs>
{
    private const string Command = "/cosmeticos";

    /// <inheritdoc />
    public override string Key => Command;

    /// <inheritdoc />
    public override CharacterStatus MinCharacterStatusRequirement => CharacterStatus.Normal;

    /// <inheritdoc />
    protected override async ValueTask DoHandleCommandAsync(Player player, EmptyChatCommandArgs arguments)
    {
        if (CosmeticsPlugIn.GetActivePlugIn(player) is not { } plugIn
            || await plugIn.GetPiecesAsync(player).ConfigureAwait(false) is not { } pieces)
        {
            await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.CosmeticsNotAvailable)).ConfigureAwait(false);
            return;
        }

        var unlocked = pieces.Where(p => p.IsUnlocked).ToList();
        if (unlocked.Count == 0)
        {
            await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.CosmeticListEmpty)).ConfigureAwait(false);
            return;
        }

        await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.CosmeticListHeader)).ConfigureAwait(false);
        foreach (var entry in unlocked)
        {
            var messageKey = entry.IsWorn ? nameof(PlayerMessage.CosmeticListEntryWorn) : nameof(PlayerMessage.CosmeticListEntry);
            await player.ShowLocalizedBlueMessageAsync(messageKey, entry.Definition.Number, entry.Name, entry.Definition.Slot).ConfigureAwait(false);
        }
    }
}
