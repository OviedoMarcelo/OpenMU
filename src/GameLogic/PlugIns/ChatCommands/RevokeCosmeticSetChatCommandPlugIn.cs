// <copyright file="RevokeCosmeticSetChatCommandPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlugIns.ChatCommands.Arguments;
using MUnique.OpenMU.GameLogic.PlugIns.CosmeticSets;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// A chat command plugin which removes an unlocked cosmetic set from a character, e.g. when it was given by mistake.
/// </summary>
[Guid("4E8A6C20-B3F1-4D97-A5E8-71C0F9B2D3E6")]
[PlugIn]
[Display(Name = nameof(PlugInResources.RevokeCosmeticSetChatCommandPlugIn_Name), Description = nameof(PlugInResources.RevokeCosmeticSetChatCommandPlugIn_Description), ResourceType = typeof(PlugInResources))]
[ChatCommandHelp(Command, typeof(CharacterCosmeticSetChatCommandArgs), CharacterStatus.GameMaster)]
public class RevokeCosmeticSetChatCommandPlugIn : ChatCommandPlugInBase<CharacterCosmeticSetChatCommandArgs>
{
    private const string Command = "/quitarcosmetico";

    /// <inheritdoc />
    public override string Key => Command;

    /// <inheritdoc />
    public override CharacterStatus MinCharacterStatusRequirement => CharacterStatus.GameMaster;

    /// <inheritdoc />
    protected override async ValueTask DoHandleCommandAsync(Player gameMaster, CharacterCosmeticSetChatCommandArgs arguments)
    {
        if (string.IsNullOrEmpty(arguments.CharacterName) || arguments.SetNumber <= 0)
        {
            await gameMaster.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.RevokeCosmeticSetUsage)).ConfigureAwait(false);
            return;
        }

        var player = gameMaster.GameContext.GetPlayerByCharacterName(arguments.CharacterName);
        if (player is null)
        {
            await gameMaster.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.CharacterNotFound), arguments.CharacterName).ConfigureAwait(false);
            return;
        }

        if (CosmeticSetsPlugIn.GetActivePlugIn(player) is not { } plugIn)
        {
            await gameMaster.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.CosmeticSetsNotAvailable)).ConfigureAwait(false);
            return;
        }

        var characterName = player.SelectedCharacter?.Name;
        var messageKey = await plugIn.RevokeAsync(player, arguments.SetNumber).ConfigureAwait(false)
            ? nameof(PlayerMessage.CosmeticSetRevoked)
            : nameof(PlayerMessage.CosmeticSetRevokeFailed);
        await gameMaster.ShowLocalizedBlueMessageAsync(messageKey, characterName, arguments.SetNumber).ConfigureAwait(false);
    }
}
