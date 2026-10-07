// <copyright file="AchievementsChatCommandPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlugIns.ChatCommands.Arguments;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// A chat command plugin which shows the progress of the achievements.
/// </summary>
[Guid("592DA8DF-4E43-422A-A6C4-3D89D698DF43")]
[PlugIn]
[Display(Name = nameof(PlugInResources.AchievementsChatCommandPlugIn_Name), Description = nameof(PlugInResources.AchievementsChatCommandPlugIn_Description), ResourceType = typeof(PlugInResources))]
[ChatCommandHelp(Command, typeof(EmptyChatCommandArgs), CharacterStatus.Normal)]
public class AchievementsChatCommandPlugIn : ChatCommandPlugInBase<EmptyChatCommandArgs>
{
    private const string Command = "/logros";

    /// <inheritdoc />
    public override string Key => Command;

    /// <inheritdoc />
    public override CharacterStatus MinCharacterStatusRequirement => CharacterStatus.Normal;

    /// <inheritdoc />
    protected override async ValueTask DoHandleCommandAsync(Player player, EmptyChatCommandArgs arguments)
    {
        if (TitleChatCommandPlugIn.GetActivePlugIn(player) is not { } plugIn)
        {
            await player.ShowBlueMessageAsync("Los logros no están disponibles.").ConfigureAwait(false);
            return;
        }

        if (await plugIn.GetOverviewAsync(player).ConfigureAwait(false) is not { } entries)
        {
            await player.ShowBlueMessageAsync("No se pudo cargar tu progreso. Probá de nuevo en un rato.").ConfigureAwait(false);
            return;
        }

        if (entries.Count == 0)
        {
            await player.ShowBlueMessageAsync("No hay logros activos.").ConfigureAwait(false);
            return;
        }

        foreach (var entry in entries)
        {
            var status = entry switch
            {
                { IsRewarded: true } => "[OK]",
                { IsCompleted: true } => "[Premio pendiente]",
                _ => $"{entry.Count.ToString("N0", player.Culture)}/{entry.Required.ToString("N0", player.Culture)}",
            };
            await player.ShowBlueMessageAsync($"[Logro] {entry.Achievement.Name}: {status} - {entry.Achievement.Description}").ConfigureAwait(false);
        }

        var completed = entries.Count(e => e.IsCompleted);
        await player.ShowBlueMessageAsync($"Completaste {completed} de {entries.Count} logros. Con /titulos ves tus títulos.").ConfigureAwait(false);
        if (plugIn.GetExperienceBonusPercent(player) is > 0 and var bonus)
        {
            await player.ShowBlueMessageAsync($"Tus logros te dan +{bonus.ToString("0.##", player.Culture)}% de experiencia.").ConfigureAwait(false);
        }
    }
}
