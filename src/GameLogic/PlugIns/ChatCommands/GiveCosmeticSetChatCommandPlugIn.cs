// <copyright file="GiveCosmeticSetChatCommandPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlugIns.ChatCommands.Arguments;
using MUnique.OpenMU.GameLogic.PlugIns.CosmeticSets;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// A chat command plugin which unlocks a cosmetic set for a character, e.g. as a prize of an event.
/// </summary>
[Guid("C7B1D3F9-0E62-4A85-9F4C-3B8E1D27A604")]
[PlugIn]
[Display(Name = nameof(PlugInResources.GiveCosmeticSetChatCommandPlugIn_Name), Description = nameof(PlugInResources.GiveCosmeticSetChatCommandPlugIn_Description), ResourceType = typeof(PlugInResources))]
[ChatCommandHelp(Command, typeof(CharacterCosmeticSetChatCommandArgs), CharacterStatus.GameMaster)]
public class GiveCosmeticSetChatCommandPlugIn : ChatCommandPlugInBase<CharacterCosmeticSetChatCommandArgs>
{
    private const string Command = "/darcosmetico";

    /// <inheritdoc />
    public override string Key => Command;

    /// <inheritdoc />
    public override CharacterStatus MinCharacterStatusRequirement => CharacterStatus.GameMaster;

    /// <inheritdoc />
    protected override async ValueTask DoHandleCommandAsync(Player gameMaster, CharacterCosmeticSetChatCommandArgs arguments)
    {
        if (string.IsNullOrEmpty(arguments.CharacterName) || arguments.SetNumber <= 0)
        {
            await gameMaster.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.GiveCosmeticSetUsage)).ConfigureAwait(false);
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

        if (await plugIn.GrantAsync(player, arguments.SetNumber, "gm").ConfigureAwait(false) is not { } set)
        {
            await gameMaster.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.CosmeticSetGiveFailed), arguments.SetNumber).ConfigureAwait(false);
            return;
        }

        await gameMaster.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.CosmeticSetGiven), player.SelectedCharacter?.Name, set.Name).ConfigureAwait(false);
    }
}
