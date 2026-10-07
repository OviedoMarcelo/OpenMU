// <copyright file="PrestigeChatCommandPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlugIns.ChatCommands.Arguments;
using MUnique.OpenMU.GameLogic.PlugIns.Prestige;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// A chat command plugin which shows the prestige of the character, and does it with "/prestigio confirmar".
/// </summary>
[Guid("F102FF3B-F9EB-43F2-99C0-B62F633F45E7")]
[PlugIn]
[Display(Name = nameof(PlugInResources.PrestigeChatCommandPlugIn_Name), Description = nameof(PlugInResources.PrestigeChatCommandPlugIn_Description), ResourceType = typeof(PlugInResources))]
[ChatCommandHelp(Command, typeof(PrestigeChatCommandArgs), CharacterStatus.Normal)]
public class PrestigeChatCommandPlugIn : ChatCommandPlugInBase<PrestigeChatCommandArgs>
{
    private const string Command = "/prestigio";

    private const string ConfirmAction = "confirmar";

    /// <inheritdoc />
    public override string Key => Command;

    /// <inheritdoc />
    public override CharacterStatus MinCharacterStatusRequirement => CharacterStatus.Normal;

    /// <inheritdoc />
    protected override async ValueTask DoHandleCommandAsync(Player player, PrestigeChatCommandArgs arguments)
    {
        if (player.GameContext.PlugInManager.GetActivePlugInsOf<IExperienceCalculationPlugIn>().OfType<PrestigePlugIn>().FirstOrDefault() is not { } plugIn
            || plugIn.Configuration is not { } configuration)
        {
            await player.ShowBlueMessageAsync("El prestigio no está disponible.").ConfigureAwait(false);
            return;
        }

        if (await plugIn.GetPrestigeAsync(player).ConfigureAwait(false) is not { } prestige)
        {
            await player.ShowBlueMessageAsync("No se pudo cargar tu prestigio. Probá de nuevo en un rato.").ConfigureAwait(false);
            return;
        }

        if (string.Equals(arguments.Action, ConfirmAction, StringComparison.OrdinalIgnoreCase))
        {
            var result = await plugIn.PrestigeAsync(player).ConfigureAwait(false);
            if (result != PrestigeResult.Done)
            {
                await player.ShowBlueMessageAsync(GetReason(result, plugIn, configuration, player)).ConfigureAwait(false);
            }

            return;
        }

        var bonus = configuration.GetExperienceBonusPercent(prestige.Level);
        await player.ShowBlueMessageAsync($"[Prestigio] Nivel {prestige.Level}, {prestige.Points} punto(s), +{bonus.ToString("0.##", player.Culture)}% de experiencia.").ConfigureAwait(false);

        var check = plugIn.CheckRequirements(player, prestige.Level);
        if (check != PrestigeResult.Done)
        {
            await player.ShowBlueMessageAsync(GetReason(check, plugIn, configuration, player)).ConfigureAwait(false);
            return;
        }

        var nextBonus = configuration.GetExperienceBonusPercent(prestige.Level + 1);
        await player.ShowBlueMessageAsync($"[Prestigio] Podés pasar al prestigio {prestige.Level + 1}: tu nivel master, su experiencia y el árbol master vuelven a 0 (los resets no cambian), y tu bonus pasa a +{nextBonus.ToString("0.##", player.Culture)}%.").ConfigureAwait(false);
        await player.ShowBlueMessageAsync("[Prestigio] Para hacerlo escribí /prestigio confirmar. Vas a volver a la selección de personajes.").ConfigureAwait(false);
    }

    private static string GetReason(PrestigeResult result, PrestigePlugIn plugIn, PrestigeConfiguration configuration, Player player) => result switch
    {
        PrestigeResult.LevelTooLow => $"[Prestigio] Necesitás nivel {configuration.RequiredLevel}.",
        PrestigeResult.MasterLevelTooLow => $"[Prestigio] Necesitás nivel master {plugIn.GetRequiredMasterLevel(player)}.",
        PrestigeResult.MaximumReached => "[Prestigio] Ya llegaste al prestigio máximo.",
        PrestigeResult.RewardsDontFit => "[Prestigio] Liberá espacio en el inventario para recibir los premios del prestigio.",
        _ => "[Prestigio] No se pudo hacer el prestigio. Probá de nuevo en un rato.",
    };
}
