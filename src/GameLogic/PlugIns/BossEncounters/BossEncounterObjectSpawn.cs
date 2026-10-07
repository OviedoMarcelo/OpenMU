// <copyright file="BossEncounterObjectSpawn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.BossEncounters;

/// <summary>
/// An object of a phase, like a pillar or a crystal, which the players have to destroy.
/// It doesn't move and doesn't attack by itself.
/// </summary>
public class BossEncounterObjectSpawn
{
    /// <summary>
    /// Gets or sets the number of the monster which represents the object.
    /// </summary>
    [Display(Name = "Número de monstruo", Description = "El monstruo que representa al objeto (p. ej. un huevo de araña de Raklion). No se mueve ni ataca.")]
    public short MonsterNumber { get; set; }

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

    /// <summary>
    /// Gets or sets the health of the object. 0 means the health of the monster definition.
    /// </summary>
    [Display(Name = "Vida", Description = "0 = la vida del monstruo.")]
    [Range(0, int.MaxValue)]
    public int Health { get; set; }

    /// <inheritdoc />
    public override string ToString() => $"{this.MonsterNumber} ({this.X}, {this.Y})";
}
