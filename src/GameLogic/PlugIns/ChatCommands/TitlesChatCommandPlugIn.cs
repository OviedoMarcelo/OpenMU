// <copyright file="TitlesChatCommandPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlugIns.ChatCommands.Arguments;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// A chat command plugin which shows the unlocked titles.
/// </summary>
[Guid("7B8E3FF9-6A62-4659-AC5A-82D608C83BAB")]
[PlugIn]
[Display(Name = nameof(PlugInResources.TitlesChatCommandPlugIn_Name), Description = nameof(PlugInResources.TitlesChatCommandPlugIn_Description), ResourceType = typeof(PlugInResources))]
[ChatCommandHelp(Command, typeof(EmptyChatCommandArgs), CharacterStatus.Normal)]
public class TitlesChatCommandPlugIn : ChatCommandPlugInBase<EmptyChatCommandArgs>
{
    private const string Command = "/titulos";

    /// <inheritdoc />
    public override string Key => Command;

    /// <inheritdoc />
    public override CharacterStatus MinCharacterStatusRequirement => CharacterStatus.Normal;

    /// <inheritdoc />
    protected override async ValueTask DoHandleCommandAsync(Player player, EmptyChatCommandArgs arguments)
    {
        if (TitleChatCommandPlugIn.GetActivePlugIn(player) is not { } plugIn)
        {
            await player.ShowBlueMessageAsync("Los títulos no están disponibles.").ConfigureAwait(false);
            return;
        }

        if (await plugIn.GetTitlesAsync(player).ConfigureAwait(false) is not { } titles)
        {
            await player.ShowBlueMessageAsync("No se pudieron cargar tus títulos. Probá de nuevo en un rato.").ConfigureAwait(false);
            return;
        }

        if (titles.Unlocked.Count == 0)
        {
            await player.ShowBlueMessageAsync("Todavía no tenés títulos. Completá logros (/logros) para conseguirlos.").ConfigureAwait(false);
            return;
        }

        foreach (var title in titles.Unlocked)
        {
            var mark = title == titles.Active ? " (activo)" : string.Empty;
            await player.ShowBlueMessageAsync($"[Título] {title.Text} - /titulo {title.Id}{mark}").ConfigureAwait(false);
        }

        await player.ShowBlueMessageAsync("Con /titulo off dejás de mostrar el título.").ConfigureAwait(false);
    }
}
