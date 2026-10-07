// <copyright file="BossEncounterChatCommandPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlugIns.BossEncounters;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// A chat command for game masters, which shows the boss encounters and starts or ends one of them.
/// </summary>
/// <remarks>
/// <c>/boss</c> lists the encounters, <c>/boss iniciar &lt;name&gt;</c> starts one and
/// <c>/boss terminar &lt;name&gt;</c> lets the boss retreat. The name may contain spaces.
/// </remarks>
[Guid("1F2F49FB-D780-4E1D-9D81-2D25A4A33F7C")]
[PlugIn]
[Display(Name = nameof(PlugInResources.BossEncounterChatCommandPlugIn_Name), Description = nameof(PlugInResources.BossEncounterChatCommandPlugIn_Description), ResourceType = typeof(PlugInResources))]
[ChatCommandHelp(CommandKey, CharacterStatus.GameMaster)]
public class BossEncounterChatCommandPlugIn : IChatCommandPlugIn
{
    private const string CommandKey = "/boss";
    private const string StartAction = "iniciar";
    private const string StopAction = "terminar";

    /// <inheritdoc />
    public string Key => CommandKey;

    /// <inheritdoc />
    public CharacterStatus MinCharacterStatusRequirement => CharacterStatus.GameMaster;

    /// <inheritdoc />
    public async ValueTask HandleCommandAsync(Player player, string command)
    {
        var encounters = BossEncounterPlugIn.GetEncounters(player.GameContext);
        var arguments = command.Length > CommandKey.Length ? command[CommandKey.Length..].Trim() : string.Empty;
        if (arguments.Length == 0)
        {
            if (encounters.Count == 0)
            {
                await player.ShowBlueMessageAsync("[Boss] No hay encuentros. ¿Está activo el plugin de encuentros con bosses?").ConfigureAwait(false);
                return;
            }

            foreach (var encounter in encounters)
            {
                await player.ShowBlueMessageAsync($"[Boss] {encounter.GetStatusText()}").ConfigureAwait(false);
            }

            return;
        }

        var separator = arguments.IndexOf(' ');
        var action = separator < 0 ? arguments : arguments[..separator];
        var name = separator < 0 ? string.Empty : arguments[(separator + 1)..].Trim();
        var isStart = string.Equals(action, StartAction, StringComparison.OrdinalIgnoreCase);
        var isStop = string.Equals(action, StopAction, StringComparison.OrdinalIgnoreCase);
        if ((!isStart && !isStop) || name.Length == 0)
        {
            await player.ShowBlueMessageAsync($"[Boss] Usá {CommandKey}, {CommandKey} {StartAction} <nombre> o {CommandKey} {StopAction} <nombre>.").ConfigureAwait(false);
            return;
        }

        if (encounters.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase)) is not { } context)
        {
            await player.ShowBlueMessageAsync($"[Boss] No existe el encuentro \"{name}\".").ConfigureAwait(false);
            return;
        }

        if (isStart)
        {
            if (context.IsRunning)
            {
                await player.ShowBlueMessageAsync($"[Boss] {context.Name} ya está en curso.").ConfigureAwait(false);
                return;
            }

            context.RequestStart();
            await player.ShowBlueMessageAsync($"[Boss] {context.Name} empieza en un segundo.").ConfigureAwait(false);
            return;
        }

        if (!context.IsRunning)
        {
            await player.ShowBlueMessageAsync($"[Boss] {context.Name} no está en curso.").ConfigureAwait(false);
            return;
        }

        context.RequestStop();
        await player.ShowBlueMessageAsync($"[Boss] {context.Name} se retira en un segundo.").ConfigureAwait(false);
    }
}
