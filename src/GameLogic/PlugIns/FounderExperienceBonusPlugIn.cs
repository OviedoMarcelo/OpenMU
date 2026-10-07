// <copyright file="FounderExperienceBonusPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlugIns.Achievements;
using MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Rewards the accounts registered before a cutoff date (e.g. everyone who signed up before the beta opened):
/// a small experience bonus, and a "Founder" title for their characters.
/// </summary>
[PlugIn]
[Display(Name = "Founder Experience Bonus", Description = "Gives a small experience bonus and a title to accounts registered before a configured cutoff date (e.g. everyone who signed up before the beta).")]
[Guid("9F2C7B7E-8B1A-4C2D-9E3F-6D5A0C8B4F21")]
public class FounderExperienceBonusPlugIn : IExperienceCalculationPlugIn, IPlayerStateChangedPlugIn, ISupportCustomConfiguration<FounderBonusConfiguration>, ISupportDefaultCustomConfiguration, IDisabledByDefault
{
    /// <inheritdoc />
    public FounderBonusConfiguration? Configuration { get; set; }

    /// <inheritdoc />
    public object CreateDefaultConfig() => FounderBonusConfiguration.Default;

    /// <inheritdoc />
    public ValueTask CalculateExperienceAsync(Player player, ExperienceCalculationArgs args)
    {
        var configuration = this.Configuration;
        if (configuration is null || configuration.BonusMultiplier == 1f)
        {
            return ValueTask.CompletedTask;
        }

        if (player.Account is { } account && account.RegistrationDate < configuration.CutoffDate)
        {
            args.Experience *= configuration.BonusMultiplier;
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        if (previousState != PlayerState.CharacterSelection
            || currentState != PlayerState.EnteredWorld
            || this.Configuration is not { TitleId.Length: > 0 } configuration
            || player.Account is not { } account
            || account.RegistrationDate >= configuration.CutoffDate
            || IsGameMaster(player)
            || TitleChatCommandPlugIn.GetActivePlugIn(player) is not { } achievements)
        {
            return;
        }

        try
        {
            // Null when the character already had it (or the title isn't configured): nothing to tell then.
            if (await achievements.GrantTitleAsync(player, configuration.TitleId, "founder").ConfigureAwait(false) is not { } title)
            {
                return;
            }

            // Shown right away unless the character already shows another title.
            if (await achievements.GetTitlesAsync(player).ConfigureAwait(false) is { Active: null })
            {
                await achievements.SetActiveTitleAsync(player, title.Id).ConfigureAwait(false);
            }

            await player.ShowBlueMessageAsync($"¡Gracias por estar desde el principio! Tu personaje recibió el título {title.Text}. Podés cambiarlo con /titulo.").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "Couldn't give the founder title to {character}.", player.SelectedCharacter?.Name);
        }
    }

    private static bool IsGameMaster(Player player)
    {
        return player.Account?.State is AccountState.GameMaster or AccountState.GameMasterInvisible
               || player.SelectedCharacter?.CharacterStatus == CharacterStatus.GameMaster;
    }
}
