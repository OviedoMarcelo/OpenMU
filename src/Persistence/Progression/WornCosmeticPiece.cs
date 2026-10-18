// <copyright file="WornCosmeticPiece.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Progression;

/// <summary>
/// A cosmetic piece which a character wears over the item of an armor slot.
/// </summary>
public class WornCosmeticPiece
{
    /// <summary>
    /// Gets or sets the identifier of the character.
    /// </summary>
    public Guid CharacterId { get; set; }

    /// <summary>
    /// Gets or sets the item slot over which the piece is worn, e.g. the one of the helm.
    /// </summary>
    public byte ItemSlot { get; set; }

    /// <summary>
    /// Gets or sets the number of the piece, as configured in the plugin configuration.
    /// </summary>
    public short PieceNumber { get; set; }
}
