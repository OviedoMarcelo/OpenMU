// <copyright file="ShowGroundMarkerPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.World;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.Views.World;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using MUnique.OpenMU.Network.PlugIns;
using MUnique.OpenMU.Pathfinding;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IShowGroundMarkerPlugIn"/> which sends a
/// <see cref="Network.Packets.ServerToClient.GroundMarker"/> message to the game client.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.ShowGroundMarkerPlugIn_Name), Description = nameof(PlugInResources.ShowGroundMarkerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("A6C1F3D2-5B7E-4E9A-8F21-3C4D6B8A9E15")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public class ShowGroundMarkerPlugIn : IShowGroundMarkerPlugIn
{
    private const byte WarningKind = 0;
    private const byte HitKind = 1;

    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowGroundMarkerPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ShowGroundMarkerPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask ShowGroundWarningAsync(Point center, byte radius, TimeSpan duration)
    {
        if (this._player.Connection is not { Connected: true } connection)
        {
            return;
        }

        var milliseconds = (ushort)Math.Clamp(duration.TotalMilliseconds, 0, ushort.MaxValue);
        await connection.SendGroundMarkerAsync(WarningKind, center.X, center.Y, radius, milliseconds).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask ShowGroundHitAsync(Point center, byte radius)
    {
        if (this._player.Connection is not { Connected: true } connection)
        {
            return;
        }

        await connection.SendGroundMarkerAsync(HitKind, center.X, center.Y, radius, 0).ConfigureAwait(false);
    }
}
