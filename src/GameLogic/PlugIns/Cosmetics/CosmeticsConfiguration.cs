// <copyright file="CosmeticsConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.Cosmetics;

using MUnique.OpenMU.DataModel.Composition;
using MUnique.OpenMU.Interfaces;

/// <summary>
/// The configuration of the <see cref="CosmeticsPlugIn"/>.
/// </summary>
public class CosmeticsConfiguration
{
    /// <summary>
    /// The number of the first piece of each set of the default configuration, multiplied with the index of the set.
    /// </summary>
    private const short DefaultNumbersPerSet = 10;

    /// <summary>
    /// Gets the default configuration: the pieces of a few classic armor sets.
    /// The pieces of a set have the numbers of the set times 10, plus 1 for the helm to 5 for the boots.
    /// </summary>
    public static CosmeticsConfiguration Default
    {
        get
        {
            // The armor sets: their item number, the level they show and whether they glow like excellent items.
            (short ItemNumber, byte Level, bool IsExcellent)[] sets =
            [
                (1, 9, false), // Dragon
                (3, 9, false), // Legendary
                (16, 11, false), // Black Dragon
                (18, 11, false), // Grand Soul
                (19, 11, false), // Divine
                (21, 13, true), // Great Dragon
                (22, 13, true), // Dark Soul
                (24, 13, true), // Red Spirit
            ];

            var configuration = new CosmeticsConfiguration();
            for (var i = 0; i < sets.Length; i++)
            {
                foreach (var slot in CosmeticSlotExtensions.ArmorSlots)
                {
                    configuration.Pieces.Add(new CosmeticPieceDefinition
                    {
                        Number = (short)(((i + 1) * DefaultNumbersPerSet) + (int)slot + 1),
                        Slot = slot,
                        ItemNumber = sets[i].ItemNumber,
                        Level = sets[i].Level,
                        IsExcellent = sets[i].IsExcellent,
                    });
                }
            }

            return configuration;
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether a character can only wear pieces which its class could equip.
    /// </summary>
    /// <remarks>
    /// The armor models of some classes don't fit to others, e.g. a piece of the Fairy Elf on a Dark Knight.
    /// </remarks>
    [Display(Name = "Solo piezas de la clase", Description = "Un personaje solo puede usar piezas que su clase podría equiparse. Desactivarlo permite combinaciones que pueden verse mal.")]
    public bool RestrictToQualifiedClasses { get; set; } = true;

    /// <summary>
    /// Gets or sets the message which is shown when a piece has been unlocked.
    /// Placeholders: {0} = name of the piece, {1} = number of the piece.
    /// </summary>
    [Display(Name = "Mensaje de pieza desbloqueada", Description = "{0} = nombre de la pieza, {1} = número de la pieza.")]
    public LocalizedString UnlockedMessage { get; set; } = "Nueva pieza cosmética: {0}. Usala desde el inventario.";

    /// <summary>
    /// Gets or sets the pieces.
    /// </summary>
    [Display(Name = "Piezas", Description = "Las piezas cosméticas. Se guardan por el Número, así que no lo cambies en una pieza que ya tiene alguien.")]
    [MemberOfAggregate]
    [ScaffoldColumn(true)]
    public ICollection<CosmeticPieceDefinition> Pieces { get; set; } = new List<CosmeticPieceDefinition>();

    /// <summary>
    /// Finds the piece with the specified number.
    /// </summary>
    /// <param name="number">The number of the piece.</param>
    /// <returns>The piece; <c>null</c>, if there is none with this number.</returns>
    public CosmeticPieceDefinition? FindPiece(short? number)
    {
        return number is { } n ? this.Pieces.FirstOrDefault(p => p.Number == n) : null;
    }
}
