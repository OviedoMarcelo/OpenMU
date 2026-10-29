// <copyright file="ResetMasterSkillsChatCommandPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlayerActions;
using MUnique.OpenMU.GameLogic.Views.Login;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// A chat command plugin which empties the master skill tree of a character and gives back the spent master points.
/// The master level and the master experience stay as they are.
/// </summary>
[Guid("E84B5598-A884-49EC-9B7E-20C1541F6398")]
[PlugIn]
[Display(Name = nameof(PlugInResources.ResetMasterSkillsChatCommandPlugIn_Name), Description = nameof(PlugInResources.ResetMasterSkillsChatCommandPlugIn_Description), ResourceType = typeof(PlugInResources))]
[ChatCommandHelp(Command, typeof(Arguments), MinimumStatus)]
public class ResetMasterSkillsChatCommandPlugIn : ChatCommandPlugInBase<ResetMasterSkillsChatCommandPlugIn.Arguments>
{
    private const string Command = "/resetmaster";
    private const CharacterStatus MinimumStatus = CharacterStatus.GameMaster;

    /// <inheritdoc />
    public override string Key => Command;

    /// <inheritdoc />
    public override CharacterStatus MinCharacterStatusRequirement => MinimumStatus;

    /// <inheritdoc />
    protected override async ValueTask DoHandleCommandAsync(Player player, Arguments arguments)
    {
        var targetPlayer = player;
        if (arguments?.CharacterName is { Length: > 0 } characterName)
        {
            targetPlayer = player.GameContext.GetPlayerByCharacterName(characterName);
            if (targetPlayer?.SelectedCharacter is null
                || !targetPlayer.SelectedCharacter.Name.Equals(characterName, StringComparison.OrdinalIgnoreCase))
            {
                await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.CharacterNotFound), characterName).ConfigureAwait(false);
                return;
            }
        }

        if (targetPlayer.SelectedCharacter is not { } character
            || targetPlayer.PlayerState.CurrentState != PlayerState.EnteredWorld)
        {
            return;
        }

        var masterSkills = character.LearnedSkills.Where(s => s.Skill?.MasterDefinition is not null).ToList();

        // The points spent on a master skill are its level: learning it costs its minimum level, and each further level one point.
        var refundedPoints = masterSkills.Sum(s => s.Level);
        foreach (var masterSkill in masterSkills)
        {
            character.LearnedSkills.Remove(masterSkill);
            await targetPlayer.PersistenceContext.DeleteAsync(masterSkill).ConfigureAwait(false);
        }

        character.MasterLevelUpPoints = checked(character.MasterLevelUpPoints + refundedPoints);
        targetPlayer.Logger.LogInformation(
            "Master skill tree of character {character} reset by {gameMaster}: {skills} skills removed, {points} points given back.",
            character.Name,
            player.SelectedCharacter?.Name,
            masterSkills.Count,
            refundedPoints);

        await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.MasterSkillsResetResult), character.Name, refundedPoints, character.MasterLevelUpPoints).ConfigureAwait(false);

        // Like after a prestige: the client only shows the empty master skill tree after loading the character again.
        await new LogoutAction().LogoutAsync(targetPlayer, LogoutType.BackToCharacterSelection).ConfigureAwait(false);
    }

    /// <summary>
    /// Arguments for the reset master skills chat command.
    /// </summary>
    public class Arguments : ArgumentsBase
    {
        /// <summary>
        /// Gets or sets the name of the character whose master skill tree is reset. Empty for the own character.
        /// </summary>
        public string? CharacterName { get; set; }
    }
}
