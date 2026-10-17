// <copyright file="ActiveCosmeticSet.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Progression;

/// <summary>
/// The cosmetic set which a character wears over its armor.
/// </summary>
public class ActiveCosmeticSet
{
    /// <summary>
    /// Gets or sets the identifier of the character.
    /// </summary>
    public Guid CharacterId { get; set; }

    /// <summary>
    /// Gets or sets the number of the set, as configured in the plugin configuration.
    /// </summary>
    public short SetNumber { get; set; }
}
