// <copyright file="ChristmasEventConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChristmasEvent;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// The configuration of the Christmas event: when it runs, the gift of Santa Claus and the buffs of the Little Santas.
/// </summary>
public class ChristmasEventConfiguration
{
    /// <summary>
    /// Gets or sets the start of the event, in the time zone of the server.
    /// </summary>
    [Display(Name = "Start", Description = "Cuándo empieza el evento, en la hora del servidor. Antes no se puede entrar a Santa Village ni recibir el regalo.")]
    public DateTime Start { get; set; } = new(2026, 12, 1, 0, 0, 0, DateTimeKind.Unspecified);

    /// <summary>
    /// Gets or sets the end of the event (exclusive), in the time zone of the server.
    /// </summary>
    [Display(Name = "End", Description = "Cuándo termina el evento (sin incluir), en la hora del servidor.")]
    public DateTime End { get; set; } = new(2027, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

    /// <summary>
    /// Gets or sets the group of the item which Santa Claus gives.
    /// </summary>
    [Display(Name = "Gift Item Group", Description = "Grupo del ítem que regala Santa. 14 = Christmas Star.")]
    [Range(0, 15)]
    public byte GiftItemGroup { get; set; } = 14;

    /// <summary>
    /// Gets or sets the number of the item which Santa Claus gives.
    /// </summary>
    [Display(Name = "Gift Item Number", Description = "Número del ítem que regala Santa. 51 = Christmas Star.")]
    [Range(0, 511)]
    public short GiftItemNumber { get; set; } = 51;

    /// <summary>
    /// Gets or sets the level of the item which Santa Claus gives.
    /// </summary>
    [Display(Name = "Gift Item Level", Description = "Nivel del ítem que regala Santa. La Christmas Star +1 es la caja con los premios especiales.")]
    [Range(0, 15)]
    public byte GiftItemLevel { get; set; } = 1;

    /// <summary>
    /// Gets or sets a value indicating whether the gift is limited per IP address, too, besides per account.
    /// </summary>
    [Display(Name = "Limit Per IP Address", Description = "Si está activo, Santa da un solo regalo por día por IP (por hogar), además de uno por cuenta.")]
    public bool LimitPerIpAddress { get; set; } = true;

    /// <summary>
    /// Gets or sets the duration of the buffs of the Little Santas.
    /// </summary>
    [Display(Name = "Buff Duration", Description = "Cuánto duran los buffs de los Little Santa.")]
    public TimeSpan BuffDuration { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Determines whether the event is running at the specified point in time.
    /// </summary>
    /// <param name="serverTime">The point in time, in the time zone of the server.</param>
    /// <returns><c>true</c>, if the event is running.</returns>
    public bool IsRunning(DateTime serverTime) => serverTime >= this.Start && serverTime < this.End;
}
