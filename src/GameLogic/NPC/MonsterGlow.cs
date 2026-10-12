// <copyright file="MonsterGlow.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.NPC;

/// <summary>
/// The glow with which the client draws a monster, like the glow of an item of a high level.
/// </summary>
/// <remarks>
/// The values are stored in configurations and sent to the client, so they must not be renumbered.
/// The levels match the item levels whose glow they look like.
/// </remarks>
public enum MonsterGlow : byte
{
    /// <summary>
    /// No glow.
    /// </summary>
    [Display(Name = "Ninguno")]
    None = 0,

    /// <summary>
    /// Like an item +7.
    /// </summary>
    [Display(Name = "+7")]
    Level7 = 7,

    /// <summary>
    /// Like an item +9.
    /// </summary>
    [Display(Name = "+9")]
    Level9 = 9,

    /// <summary>
    /// Like an item +11.
    /// </summary>
    [Display(Name = "+11")]
    Level11 = 11,

    /// <summary>
    /// Like an item +13.
    /// </summary>
    [Display(Name = "+13")]
    Level13 = 13,

    /// <summary>
    /// Like an item +15.
    /// </summary>
    [Display(Name = "+15")]
    Level15 = 15,

    /// <summary>
    /// Golden, like the monsters of the golden invasion.
    /// </summary>
    [Display(Name = "Dorado")]
    Golden = 100,
}
