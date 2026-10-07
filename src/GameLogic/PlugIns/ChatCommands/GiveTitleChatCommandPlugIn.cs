// <copyright file="GiveTitleChatCommandPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlugIns.ChatCommands.Arguments;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// A chat command plugin which unlocks a title for a character, e.g. as a prize of an event.
/// </summary>
[Guid("E03B8D23-1D31-402E-9958-35414CE3461D")]
[PlugIn]
[Display(Name = nameof(PlugInResources.GiveTitleChatCommandPlugIn_Name), Description = nameof(PlugInResources.GiveTitleChatCommandPlugIn_Description), ResourceType = typeof(PlugInResources))]
[ChatCommandHelp(Command, typeof(GiveTitleChatCommandArgs), CharacterStatus.GameMaster)]
public class GiveTitleChatCommandPlugIn : ChatCommandPlugInBase<GiveTitleChatCommandArgs>
{
    private const string Command = "/daretitulo";

    /// <inheritdoc />
    public override string Key => Command;

    /// <inheritdoc />
    public override CharacterStatus MinCharacterStatusRequirement => CharacterStatus.GameMaster;

    /// <inheritdoc />
    protected override async ValueTask DoHandleCommandAsync(Player gameMaster, GiveTitleChatCommandArgs arguments)
    {
        if (string.IsNullOrEmpty(arguments.CharacterName) || string.IsNullOrEmpty(arguments.TitleId))
        {
            await gameMaster.ShowBlueMessageAsync("Usá /daretitulo <personaje> <id>.").ConfigureAwait(false);
            return;
        }

        var player = gameMaster.GameContext.GetPlayerByCharacterName(arguments.CharacterName);
        if (player is null)
        {
            await gameMaster.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.CharacterNotFound), arguments.CharacterName).ConfigureAwait(false);
            return;
        }

        if (TitleChatCommandPlugIn.GetActivePlugIn(player) is not { } plugIn)
        {
            await gameMaster.ShowBlueMessageAsync("Los títulos no están disponibles.").ConfigureAwait(false);
            return;
        }

        if (await plugIn.GrantTitleAsync(player, arguments.TitleId, "gm").ConfigureAwait(false) is not { } title)
        {
            await gameMaster.ShowBlueMessageAsync($"No se pudo dar el título \"{arguments.TitleId}\". Revisá que el id exista en la configuración del plugin Achievements.").ConfigureAwait(false);
            return;
        }

        await gameMaster.ShowBlueMessageAsync($"{player.SelectedCharacter?.Name} desbloqueó el título {title.Text}.").ConfigureAwait(false);
    }
}
