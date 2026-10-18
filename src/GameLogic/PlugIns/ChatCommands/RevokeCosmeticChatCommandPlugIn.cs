// <copyright file="RevokeCosmeticChatCommandPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlugIns.ChatCommands.Arguments;
using MUnique.OpenMU.GameLogic.PlugIns.Cosmetics;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// A chat command plugin which removes an unlocked cosmetic piece from a character, e.g. when it was given by mistake.
/// </summary>
[Guid("4E8A6C20-B3F1-4D97-A5E8-71C0F9B2D3E6")]
[PlugIn]
[Display(Name = nameof(PlugInResources.RevokeCosmeticChatCommandPlugIn_Name), Description = nameof(PlugInResources.RevokeCosmeticChatCommandPlugIn_Description), ResourceType = typeof(PlugInResources))]
[ChatCommandHelp(Command, typeof(CharacterCosmeticChatCommandArgs), CharacterStatus.GameMaster)]
public class RevokeCosmeticChatCommandPlugIn : ChatCommandPlugInBase<CharacterCosmeticChatCommandArgs>
{
    private const string Command = "/quitarcosmetico";

    /// <inheritdoc />
    public override string Key => Command;

    /// <inheritdoc />
    public override CharacterStatus MinCharacterStatusRequirement => CharacterStatus.GameMaster;

    /// <inheritdoc />
    protected override async ValueTask DoHandleCommandAsync(Player gameMaster, CharacterCosmeticChatCommandArgs arguments)
    {
        if (string.IsNullOrEmpty(arguments.CharacterName) || arguments.PieceNumber <= 0)
        {
            await gameMaster.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.RevokeCosmeticUsage)).ConfigureAwait(false);
            return;
        }

        var player = gameMaster.GameContext.GetPlayerByCharacterName(arguments.CharacterName);
        if (player is null)
        {
            await gameMaster.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.CharacterNotFound), arguments.CharacterName).ConfigureAwait(false);
            return;
        }

        if (CosmeticsPlugIn.GetActivePlugIn(player) is not { } plugIn)
        {
            await gameMaster.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.CosmeticsNotAvailable)).ConfigureAwait(false);
            return;
        }

        var characterName = player.SelectedCharacter?.Name;
        var messageKey = await plugIn.RevokeAsync(player, arguments.PieceNumber).ConfigureAwait(false)
            ? nameof(PlayerMessage.CosmeticRevoked)
            : nameof(PlayerMessage.CosmeticRevokeFailed);
        await gameMaster.ShowLocalizedBlueMessageAsync(messageKey, characterName, arguments.PieceNumber).ConfigureAwait(false);
    }
}
