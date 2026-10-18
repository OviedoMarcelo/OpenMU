// <copyright file="CosmeticChatCommandArgs.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChatCommands.Arguments;

/// <summary>
/// Arguments for the chat command which puts on or takes off a cosmetic piece of the character.
/// </summary>
public class CosmeticChatCommandArgs : ArgumentsBase
{
    /// <summary>
    /// Gets or sets the number of the piece which is put on, or "off" to take off pieces.
    /// </summary>
    [Argument("number")]
    public string? Piece { get; set; }

    /// <summary>
    /// Gets or sets the number of the piece which is taken off, after "off"; 0 for all.
    /// </summary>
    [Argument("pieceNumber", false)]
    public short TakeOffPieceNumber { get; set; }
}
