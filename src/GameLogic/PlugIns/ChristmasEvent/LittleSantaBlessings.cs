// <copyright file="LittleSantaBlessings.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChristmasEvent;

/// <summary>
/// What each Little Santa of Santa Village gives. The numbers of the effects are the ones of the buffs of the
/// client (eBuff_BlessingOfXmax = 91 up to eBuff_LuckOfSanta = 97 in MuMain), which shows their texts.
/// </summary>
public static class LittleSantaBlessings
{
    /// <summary>The effect "Cure of Santa": maximum health +500.</summary>
    public const short CureOfSanta = 92;

    /// <summary>The effect "Safeguard of Santa": maximum mana +500.</summary>
    public const short SafeguardOfSanta = 93;

    /// <summary>The effect "Strength of Santa": attack power +30.</summary>
    public const short StrengthOfSanta = 94;

    /// <summary>The effect "Defense of Santa": defense +100.</summary>
    public const short DefenseOfSanta = 95;

    /// <summary>The effect "Quickness of Santa": attack speed +15.</summary>
    public const short QuicknessOfSanta = 96;

    /// <summary>The effect "Luck of Santa": AG recovery +10.</summary>
    public const short LuckOfSanta = 97;

    /// <summary>The Little Santa Red, which fills up the health.</summary>
    public const short RedLittleSanta = 470;

    /// <summary>The Little Santa Blue, which fills up the mana.</summary>
    public const short BlueLittleSanta = 471;

    /// <summary>
    /// Gets the effect which a Little Santa gives.
    /// </summary>
    /// <param name="littleSantaNumber">The number of the Little Santa.</param>
    /// <returns>The number of the effect; <c>null</c> for the ones which fill up health (red) or mana (blue).</returns>
    public static short? EffectOf(short littleSantaNumber) => littleSantaNumber switch
    {
        468 => StrengthOfSanta, // yellow
        469 => DefenseOfSanta, // green
        472 => CureOfSanta, // white
        473 => SafeguardOfSanta, // black
        474 => QuicknessOfSanta, // orange
        475 => LuckOfSanta, // pink
        _ => null, // red (470) and blue (471)
    };
}
