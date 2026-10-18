// <copyright file="GiveCosmeticChatCommandPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlugIns.ChatCommands.Arguments;
using MUnique.OpenMU.GameLogic.PlugIns.Cosmetics;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// A chat command plugin which unlocks a cosmetic piece for a character, e.g. as a prize of an event.
/// </summary>
[Guid("C7B1D3F9-0E62-4A85-9F4C-3B8E1D27A604")]
[PlugIn]
[Display(Name = nameof(PlugInResources.GiveCosmeticChatCommandPlugIn_Name), Description = nameof(PlugInResources.GiveCosmeticChatCommandPlugIn_Description), ResourceType = typeof(PlugInResources))]
[ChatCommandHelp(Command, typeof(CharacterCosmeticChatCommandArgs), CharacterStatus.GameMaster)]
public class GiveCosmeticChatCommandPlugIn : ChatCommandPlugInBase<CharacterCosmeticChatCommandArgs>
{
    private const string Command = "/darcosmetico";

    /// <inheritdoc />
    public override string Key => Command;

    /// <inheritdoc />
    public override CharacterStatus MinCharacterStatusRequirement => CharacterStatus.GameMaster;

    /// <inheritdoc />
    protected override async ValueTask DoHandleCommandAsync(Player gameMaster, CharacterCosmeticChatCommandArgs arguments)
    {
        if (string.IsNullOrEmpty(arguments.CharacterName) || arguments.PieceNumber <= 0)
        {
            await gameMaster.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.GiveCosmeticUsage)).ConfigureAwait(false);
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

        if (await plugIn.GrantAsync(player, arguments.PieceNumber, "gm").ConfigureAwait(false) is not { } piece)
        {
            await gameMaster.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.CosmeticGiveFailed), arguments.PieceNumber).ConfigureAwait(false);
            return;
        }

        var name = CosmeticsPlugIn.GetName(gameMaster.GameContext.Configuration, piece, gameMaster.Culture);
        await gameMaster.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.CosmeticGiven), player.SelectedCharacter?.Name, name).ConfigureAwait(false);
    }
}
