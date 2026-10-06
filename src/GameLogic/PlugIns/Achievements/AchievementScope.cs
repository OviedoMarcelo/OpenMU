// <copyright file="AchievementScope.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.Achievements;

/// <summary>
/// Defines whose progress an achievement counts.
/// </summary>
/// <remarks>
/// The values are stored in the plugin configuration, so new members have to be appended at the end.
/// </remarks>
public enum AchievementScope
{
    /// <summary>
    /// The progress belongs to the character.
    /// </summary>
    [Display(Name = "Personaje")]
    Character = 0,

    /// <summary>
    /// The progress belongs to the account: all characters of the account add to it,
    /// and its title can be shown by all of them.
    /// </summary>
    [Display(Name = "Cuenta")]
    Account = 1,
}
