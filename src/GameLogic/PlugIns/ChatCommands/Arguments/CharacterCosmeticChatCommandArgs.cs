// <copyright file="CharacterCosmeticChatCommandArgs.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChatCommands.Arguments;

/// <summary>
/// Arguments for the chat commands which give or remove a cosmetic piece of a character.
/// </summary>
public class CharacterCosmeticChatCommandArgs : ArgumentsBase
{
    /// <summary>
    /// Gets or sets the character name.
    /// </summary>
    [Argument("characterName")]
    public string? CharacterName { get; set; }

    /// <summary>
    /// Gets or sets the number of the piece.
    /// </summary>
    [Argument("number")]
    public short PieceNumber { get; set; }
}
