// <copyright file="ResetMasterSkillsChatCommandPlugInTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Moq;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlayerActions.Character;
using MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;

/// <summary>
/// Tests for the <see cref="ResetMasterSkillsChatCommandPlugIn"/>.
/// </summary>
[TestFixture]
public class ResetMasterSkillsChatCommandPlugInTest
{
    /// <summary>
    /// The master skills are removed and all the spent master points come back,
    /// while the normal skills stay.
    /// </summary>
    [Test]
    public async ValueTask GivesBackAllSpentPointsAsync()
    {
        var player = await CreateGameMasterInWorldAsync().ConfigureAwait(false);
        var character = player.SelectedCharacter!;
        var normalSkill = new Skill { Number = 100 };
        await player.SkillList!.AddLearnedSkillAsync(normalSkill).ConfigureAwait(false);

        // The second skill costs 5 points to learn, then 1 point per level.
        var first = CreateMasterSkill(1, 1, character.CharacterClass!);
        var second = CreateMasterSkill(2, 5, character.CharacterClass!);
        player.GameContext.Configuration.Skills.Add(first);
        player.GameContext.Configuration.Skills.Add(second);

        character.MasterLevelUpPoints = 20;
        var addAction = new AddMasterPointAction();
        for (var i = 0; i < 3; i++)
        {
            await addAction.AddMasterPointAsync(player, 1).ConfigureAwait(false);
        }

        await addAction.AddMasterPointAsync(player, 2).ConfigureAwait(false);
        await addAction.AddMasterPointAsync(player, 2).ConfigureAwait(false);
        Assert.That(character.MasterLevelUpPoints, Is.EqualTo(11));

        await new ResetMasterSkillsChatCommandPlugIn().HandleCommandAsync(player, "/resetmaster").ConfigureAwait(false);

        Assert.That(character.MasterLevelUpPoints, Is.EqualTo(20));
        Assert.That(character.LearnedSkills.Any(s => s.Skill?.MasterDefinition is not null), Is.False);
        Assert.That(character.LearnedSkills.Any(s => s.Skill == normalSkill), Is.True);
    }

    /// <summary>
    /// Only game masters can use the command (the chat command processor checks it before calling the command).
    /// </summary>
    [Test]
    public void OnlyForGameMasters()
    {
        Assert.That(new ResetMasterSkillsChatCommandPlugIn().MinCharacterStatusRequirement, Is.EqualTo(CharacterStatus.GameMaster));
    }

    private static async ValueTask<Player> CreateGameMasterInWorldAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.SelectedCharacter!.CharacterStatus = CharacterStatus.GameMaster;
        await player.PlayerState.TryAdvanceToAsync(PlayerState.EnteredWorld).ConfigureAwait(false);
        return player;
    }

    private static Skill CreateMasterSkill(short id, byte minimumLevel, CharacterClass characterClass)
    {
        var masterDefinition = new Mock<MasterSkillDefinition>();
        masterDefinition.SetupAllProperties();
        masterDefinition.Object.Rank = 1;
        masterDefinition.Object.MinimumLevel = minimumLevel;
        masterDefinition.Object.MaximumLevel = 20;
        masterDefinition.Object.Root = new MasterSkillRoot { Id = new Guid(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1) };
        masterDefinition.Setup(m => m.RequiredMasterSkills).Returns(new List<Skill>());

        var skill = new Mock<Skill>();
        skill.SetupAllProperties();
        skill.Object.Number = id;
        skill.Setup(s => s.QualifiedCharacters).Returns(new List<CharacterClass> { characterClass });
        skill.Object.MasterDefinition = masterDefinition.Object;
        return skill.Object;
    }
}
