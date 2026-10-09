// <copyright file="BossEncounterPoint.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.BossEncounters;

using MUnique.OpenMU.DataModel.Composition;

/// <summary>
/// A place where the boss of an encounter can appear: a map and coordinates on it.
/// </summary>
[HorizontalLayout]
public class BossEncounterPoint
{
    /// <summary>
    /// Gets or sets the number of the map.
    /// </summary>
    [Display(Name = "Mapa", Order = 1)]
    [MapNumber]
    public short MapNumber { get; set; }

    /// <summary>
    /// Gets or sets the x coordinate. When <see cref="X"/> and <see cref="Y"/> are 0, a random walkable point of the map is used.
    /// </summary>
    [Display(Name = "X", Description = "X e Y en 0 = un punto caminable al azar del mapa.", Order = 2)]
    [Range(0, 255)]
    public byte X { get; set; }

    /// <summary>
    /// Gets or sets the y coordinate. When <see cref="X"/> and <see cref="Y"/> are 0, a random walkable point of the map is used.
    /// </summary>
    [Display(Name = "Y", Order = 3)]
    [Range(0, 255)]
    public byte Y { get; set; }

    /// <summary>
    /// Determines whether a random walkable point of the map is used, instead of the coordinates.
    /// </summary>
    /// <returns><c>true</c>, if a random point is used; otherwise, <c>false</c>.</returns>
    public bool IsRandomPoint() => this.X == 0 && this.Y == 0;

    /// <inheritdoc />
    public override string ToString() => $"{this.MapNumber} ({this.X}, {this.Y})";
}
