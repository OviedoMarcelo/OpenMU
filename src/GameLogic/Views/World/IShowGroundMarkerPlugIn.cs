// <copyright file="IShowGroundMarkerPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Views.World;

using MUnique.OpenMU.Pathfinding;

/// <summary>
/// Interface of a view whose client can show where an attack of a boss hits after a delay.
/// </summary>
public interface IShowGroundMarkerPlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows a warning on the ground where an attack hits after the duration.
    /// </summary>
    /// <param name="center">The center.</param>
    /// <param name="radius">The radius, in tiles.</param>
    /// <param name="duration">The time until the attack hits.</param>
    ValueTask ShowGroundWarningAsync(Point center, byte radius, TimeSpan duration);

    /// <summary>
    /// Shows the hit of an attack which was warned about before.
    /// </summary>
    /// <param name="center">The center.</param>
    /// <param name="radius">The radius, in tiles.</param>
    ValueTask ShowGroundHitAsync(Point center, byte radius);
}
