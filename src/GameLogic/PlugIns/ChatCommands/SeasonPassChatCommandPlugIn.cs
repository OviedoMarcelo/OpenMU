// <copyright file="SeasonPassChatCommandPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlugIns.ChatCommands.Arguments;
using MUnique.OpenMU.GameLogic.PlugIns.SeasonPass;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// A chat command plugin which shows the season pass, and hands out its rewards with "/pase reclamar".
/// </summary>
[Guid("AF823FB7-402B-47BB-994F-12A8744A9BB7")]
[PlugIn]
[Display(Name = nameof(PlugInResources.SeasonPassChatCommandPlugIn_Name), Description = nameof(PlugInResources.SeasonPassChatCommandPlugIn_Description), ResourceType = typeof(PlugInResources))]
[ChatCommandHelp(Command, typeof(SeasonPassChatCommandArgs), CharacterStatus.Normal)]
public class SeasonPassChatCommandPlugIn : ChatCommandPlugInBase<SeasonPassChatCommandArgs>
{
    private const string Command = "/pase";

    private const string ClaimAction = "reclamar";

    /// <inheritdoc />
    public override string Key => Command;

    /// <inheritdoc />
    public override CharacterStatus MinCharacterStatusRequirement => CharacterStatus.Normal;

    /// <summary>
    /// Gets the season pass plugin which tracks the player, if it's active.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The plugin; <c>null</c>, if it's not active.</returns>
    internal static SeasonPassPlugIn? GetActivePlugIn(Player player)
    {
        return player.GameContext.PlugInManager.GetActivePlugInsOf<IQuestCompletedPlugIn>().OfType<SeasonPassPlugIn>().FirstOrDefault();
    }

    /// <inheritdoc />
    protected override async ValueTask DoHandleCommandAsync(Player player, SeasonPassChatCommandArgs arguments)
    {
        if (GetActivePlugIn(player) is not { } plugIn)
        {
            await player.ShowBlueMessageAsync("El pase de temporada no está disponible.").ConfigureAwait(false);
            return;
        }

        if (string.Equals(arguments.Action, ClaimAction, StringComparison.OrdinalIgnoreCase))
        {
            await ClaimAsync(player, plugIn).ConfigureAwait(false);
            return;
        }

        if (!string.IsNullOrWhiteSpace(arguments.Action))
        {
            await player.ShowBlueMessageAsync("Usá /pase para ver tu progreso o /pase reclamar para recibir los premios.").ConfigureAwait(false);
            return;
        }

        await ShowAsync(player, plugIn).ConfigureAwait(false);
    }

    private static async ValueTask ClaimAsync(Player player, SeasonPassPlugIn plugIn)
    {
        var claimed = await plugIn.ClaimAsync(player).ConfigureAwait(false);
        var message = claimed switch
        {
            null => "No hay una temporada activa, o no se pudo cargar tu pase. Probá de nuevo en un rato.",
            0 => "No tenés premios para reclamar ahora.",
            _ => null,
        };
        if (message is not null)
        {
            await player.ShowBlueMessageAsync(message).ConfigureAwait(false);
        }
    }

    private static async ValueTask ShowAsync(Player player, SeasonPassPlugIn plugIn)
    {
        if (await plugIn.GetOverviewAsync(player).ConfigureAwait(false) is not { } overview)
        {
            await player.ShowBlueMessageAsync("No se pudo cargar tu pase. Probá de nuevo en un rato.").ConfigureAwait(false);
            return;
        }

        if (overview.Season is not { } season)
        {
            await player.ShowBlueMessageAsync("No hay una temporada activa en este momento.").ConfigureAwait(false);
            return;
        }

        var maximumLevel = season.GetMaximumLevel();
        var progress = overview.Level >= maximumLevel
            ? "nivel máximo"
            : $"{overview.Experience % season.ExperiencePerLevel}/{season.ExperiencePerLevel} XP para el nivel {overview.Level + 1}";
        var premium = overview.IsPremium ? "premium" : "gratis";
        var remaining = overview.EndUtc - DateTime.UtcNow;
        await player.ShowBlueMessageAsync($"[Pase] {season.Name} ({premium}): nivel {overview.Level}/{maximumLevel}, {progress}.").ConfigureAwait(false);
        await player.ShowBlueMessageAsync($"[Pase] Termina en {Math.Max(0, (int)remaining.TotalDays)}d {Math.Max(0, remaining.Hours)}h. Ganás XP completando quests (/weekly) y jugando.").ConfigureAwait(false);
        if (overview.ClaimableCount > 0)
        {
            await player.ShowBlueMessageAsync($"[Pase] Tenés {overview.ClaimableCount} premio(s) para reclamar: /pase reclamar.").ConfigureAwait(false);
        }

        // The configuration may have changed since the client got the pass, so the window is refreshed, too.
        await plugIn.SendAsync(player).ConfigureAwait(false);
    }
}
