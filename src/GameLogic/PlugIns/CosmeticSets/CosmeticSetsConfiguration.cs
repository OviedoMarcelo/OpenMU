// <copyright file="CosmeticSetsConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.CosmeticSets;

using MUnique.OpenMU.DataModel.Composition;
using MUnique.OpenMU.Interfaces;

/// <summary>
/// The configuration of the <see cref="CosmeticSetsPlugIn"/>.
/// </summary>
public class CosmeticSetsConfiguration
{
    /// <summary>
    /// Gets the default configuration, with a few classic armor sets.
    /// </summary>
    public static CosmeticSetsConfiguration Default => new()
    {
        Sets = new List<CosmeticSetDefinition>
        {
            new() { Number = 1, Name = "Dragon", ItemNumber = 1, Level = 9 },
            new() { Number = 2, Name = "Legendary", ItemNumber = 3, Level = 9 },
            new() { Number = 3, Name = "Black Dragon", ItemNumber = 16, Level = 11 },
            new() { Number = 4, Name = "Grand Soul", ItemNumber = 18, Level = 11 },
            new() { Number = 5, Name = "Divine", ItemNumber = 19, Level = 11 },
            new() { Number = 6, Name = "Great Dragon", ItemNumber = 21, Level = 13, IsExcellent = true },
            new() { Number = 7, Name = "Dark Soul", ItemNumber = 22, Level = 13, IsExcellent = true },
            new() { Number = 8, Name = "Red Spirit", ItemNumber = 24, Level = 13, IsExcellent = true },
        },
    };

    /// <summary>
    /// Gets or sets a value indicating whether a character can only wear sets which its class could equip.
    /// </summary>
    /// <remarks>
    /// The armor models of some classes don't fit to others, e.g. a set of the Fairy Elf on a Dark Knight.
    /// </remarks>
    [Display(Name = "Solo sets de la clase", Description = "Un personaje solo puede usar sets que su clase podría equiparse. Desactivarlo permite combinaciones que pueden verse mal.")]
    public bool RestrictToQualifiedClasses { get; set; } = true;

    /// <summary>
    /// Gets or sets the message which is shown when a set has been unlocked.
    /// Placeholders: {0} = set name, {1} = set number.
    /// </summary>
    [Display(Name = "Mensaje de set desbloqueado", Description = "{0} = nombre del set, {1} = número del set.")]
    public LocalizedString UnlockedMessage { get; set; } = "Nuevo set cosmético: {0}. Usalo desde la ventana del personaje.";

    /// <summary>
    /// Gets or sets the sets.
    /// </summary>
    [Display(Name = "Sets", Description = "Los sets cosméticos. Se guardan por el Número, así que no lo cambies en un set que ya tiene alguien.")]
    [MemberOfAggregate]
    [ScaffoldColumn(true)]
    public ICollection<CosmeticSetDefinition> Sets { get; set; } = new List<CosmeticSetDefinition>();

    /// <summary>
    /// Finds the set with the specified number.
    /// </summary>
    /// <param name="number">The number of the set.</param>
    /// <returns>The set; <c>null</c>, if there is none with this number.</returns>
    public CosmeticSetDefinition? FindSet(short? number)
    {
        return number is { } n ? this.Sets.FirstOrDefault(s => s.Number == n) : null;
    }
}
