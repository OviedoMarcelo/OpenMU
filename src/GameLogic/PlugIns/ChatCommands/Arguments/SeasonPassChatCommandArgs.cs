// <copyright file="SeasonPassChatCommandArgs.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChatCommands.Arguments;

/// <summary>
/// Arguments used by the <see cref="SeasonPassChatCommandPlugIn"/>.
/// </summary>
public class SeasonPassChatCommandArgs : ArgumentsBase
{
    /// <summary>
    /// Gets or sets the action: empty to show the pass, "reclamar" to receive the rewards.
    /// </summary>
    [Argument("accion", false)]
    public string? Action { get; set; }
}
