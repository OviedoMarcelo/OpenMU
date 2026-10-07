// <copyright file="BossEncounterPoint.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.BossEncounters;

/// <summary>
/// The coordinates of a point on the map of a boss encounter.
/// </summary>
public class BossEncounterPoint
{
    /// <summary>
    /// Gets or sets the x coordinate.
    /// </summary>
    [Display(Name = "X")]
    [Range(0, 255)]
    public byte X { get; set; }

    /// <summary>
    /// Gets or sets the y coordinate.
    /// </summary>
    [Display(Name = "Y")]
    [Range(0, 255)]
    public byte Y { get; set; }

    /// <inheritdoc />
    public override string ToString() => $"({this.X}, {this.Y})";
}
