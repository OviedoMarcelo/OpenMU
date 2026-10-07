// <copyright file="PrestigeChatCommandArgs.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChatCommands.Arguments;

/// <summary>
/// Arguments used by the <see cref="PrestigeChatCommandPlugIn"/>.
/// </summary>
public class PrestigeChatCommandArgs : ArgumentsBase
{
    /// <summary>
    /// Gets or sets the action: empty to show the prestige, "confirmar" to do it.
    /// </summary>
    [Argument("accion", false)]
    public string? Action { get; set; }
}
