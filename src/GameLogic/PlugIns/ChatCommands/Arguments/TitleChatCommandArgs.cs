// <copyright file="TitleChatCommandArgs.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChatCommands.Arguments;

/// <summary>
/// Arguments used by the <see cref="TitleChatCommandPlugIn"/>.
/// </summary>
public class TitleChatCommandArgs : ArgumentsBase
{
    /// <summary>
    /// Gets or sets the id of the title, or "off" to show none.
    /// </summary>
    [Argument("id")]
    public string? TitleId { get; set; }
}
