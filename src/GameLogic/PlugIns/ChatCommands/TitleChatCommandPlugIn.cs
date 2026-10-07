// <copyright file="TitleChatCommandPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlugIns.Achievements;
using MUnique.OpenMU.GameLogic.PlugIns.ChatCommands.Arguments;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// A chat command plugin which changes the title which is shown below the name of the character.
/// </summary>
[Guid("AA4C5503-D94A-4F42-AC66-D592185CA6A4")]
[PlugIn]
[Display(Name = nameof(PlugInResources.TitleChatCommandPlugIn_Name), Description = nameof(PlugInResources.TitleChatCommandPlugIn_Description), ResourceType = typeof(PlugInResources))]
[ChatCommandHelp(Command, typeof(TitleChatCommandArgs), CharacterStatus.Normal)]
public class TitleChatCommandPlugIn : ChatCommandPlugInBase<TitleChatCommandArgs>
{
    private const string Command = "/titulo";

    private const string Off = "off";

    /// <inheritdoc />
    public override string Key => Command;

    /// <inheritdoc />
    public override CharacterStatus MinCharacterStatusRequirement => CharacterStatus.Normal;

    /// <summary>
    /// Gets the achievements plugin which tracks the player, if it's active.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The plugin; <c>null</c>, if it's not active.</returns>
    internal static AchievementsPlugIn? GetActivePlugIn(Player player)
    {
        return player.GameContext.PlugInManager.IsPlugInActive(typeof(AchievementsPlugIn).GUID)
            ? AchievementsPlugIn.GetTrackingPlugIn(player)
            : null;
    }

    /// <inheritdoc />
    protected override async ValueTask DoHandleCommandAsync(Player player, TitleChatCommandArgs arguments)
    {
        if (GetActivePlugIn(player) is not { } plugIn)
        {
            await player.ShowBlueMessageAsync("Los títulos no están disponibles.").ConfigureAwait(false);
            return;
        }

        if (string.IsNullOrWhiteSpace(arguments.TitleId))
        {
            await player.ShowBlueMessageAsync("Usá /titulo <id> o /titulo off. Con /titulos ves los tuyos.").ConfigureAwait(false);
            return;
        }

        var titleId = string.Equals(arguments.TitleId, Off, StringComparison.OrdinalIgnoreCase) ? null : arguments.TitleId;
        var message = await plugIn.SetActiveTitleAsync(player, titleId).ConfigureAwait(false) switch
        {
            TitleChangeResult.Changed => "Título cambiado.",
            TitleChangeResult.Removed => "Ya no mostrás ningún título.",
            TitleChangeResult.NotUnlocked => "Todavía no desbloqueaste ese título.",
            TitleChangeResult.Unknown => "No existe ese título. Con /titulos ves los tuyos.",
            _ => "No se pudo cambiar el título. Probá de nuevo en un rato.",
        };
        await player.ShowBlueMessageAsync(message).ConfigureAwait(false);
    }
}
