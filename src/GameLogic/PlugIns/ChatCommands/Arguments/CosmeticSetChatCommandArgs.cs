// <copyright file="CosmeticSetChatCommandArgs.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChatCommands.Arguments;

/// <summary>
/// Arguments for the chat command which changes the cosmetic set of the character.
/// </summary>
public class CosmeticSetChatCommandArgs : ArgumentsBase
{
    /// <summary>
    /// Gets or sets the number of the set, or "off" to wear none.
    /// </summary>
    [Argument("number")]
    public string? SetNumber { get; set; }
}
