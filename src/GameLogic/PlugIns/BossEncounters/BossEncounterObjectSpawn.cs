// <copyright file="BossEncounterObjectSpawn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.BossEncounters;

using MUnique.OpenMU.DataModel.Composition;

/// <summary>
/// An object of a phase, like a pillar or a crystal, which the players have to destroy.
/// It doesn't move and doesn't attack by itself. It appears on the map of the boss.
/// </summary>
[HorizontalLayout]
public class BossEncounterObjectSpawn
{
    /// <summary>
    /// Gets or sets the number of the monster which represents the object.
    /// </summary>
    [Display(Name = "Monstruo", Description = "El monstruo que representa al objeto (p. ej. un huevo de araña de Raklion). No se mueve ni ataca.", Order = 1)]
    [MonsterNumber]
    public short MonsterNumber { get; set; }

    /// <summary>
    /// Gets or sets the x coordinate.
    /// </summary>
    [Display(Name = "X", Description = "En el mapa del boss.", Order = 2)]
    [Range(0, 255)]
    public byte X { get; set; }

    /// <summary>
    /// Gets or sets the y coordinate.
    /// </summary>
    [Display(Name = "Y", Order = 3)]
    [Range(0, 255)]
    public byte Y { get; set; }

    /// <summary>
    /// Gets or sets the health of the object. 0 means the health of the monster definition.
    /// </summary>
    [Display(Name = "Vida", Description = "0 = la vida del monstruo.", Order = 4)]
    [Range(0, int.MaxValue)]
    public int Health { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the object blocks the way: the players can't walk through its tile while it's alive.
    /// </summary>
    [Display(Name = "Bloquea el paso", Description = "Los jugadores no pueden atravesar su casilla mientras no se rompa, p. ej. un pilar.", Order = 5)]
    public bool IsBlocking { get; set; }

    /// <inheritdoc />
    public override string ToString() => $"{this.MonsterNumber} ({this.X}, {this.Y})";
}
