// <copyright file="IdleConnectionDisconnectPlugInConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns;

/// <summary>
/// The configuration of the <see cref="IdleConnectionDisconnectPlugIn"/>.
/// </summary>
public class IdleConnectionDisconnectPlugInConfiguration
{
    /// <summary>
    /// Gets or sets the time without any packet of the client after which the player gets disconnected.
    /// The client sends a ping every 20 seconds, so it should be a few times that.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.IdleConnectionDisconnectPlugInConfiguration_Timeout_Name))]
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(90);
}
