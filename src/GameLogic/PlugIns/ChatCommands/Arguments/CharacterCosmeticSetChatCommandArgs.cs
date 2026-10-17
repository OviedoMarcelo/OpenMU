// <copyright file="CharacterCosmeticSetChatCommandArgs.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChatCommands.Arguments;

/// <summary>
/// Arguments for the chat commands which give or remove a cosmetic set of a character.
/// </summary>
public class CharacterCosmeticSetChatCommandArgs : ArgumentsBase
{
    /// <summary>
    /// Gets or sets the character name.
    /// </summary>
    [Argument("characterName")]
    public string? CharacterName { get; set; }

    /// <summary>
    /// Gets or sets the number of the set.
    /// </summary>
    [Argument("number")]
    public short SetNumber { get; set; }
}
