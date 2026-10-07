// <copyright file="SeasonPremiumChatCommandArgs.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChatCommands.Arguments;

/// <summary>
/// Arguments used by the <see cref="SeasonPremiumChatCommandPlugIn"/>.
/// </summary>
public class SeasonPremiumChatCommandArgs : ArgumentsBase
{
    /// <summary>
    /// Gets or sets the login name of the account.
    /// </summary>
    [Argument("login")]
    public string? LoginName { get; set; }
}
