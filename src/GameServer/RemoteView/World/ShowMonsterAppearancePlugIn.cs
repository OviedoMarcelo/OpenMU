// <copyright file="ShowMonsterAppearancePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.World;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.Views.World;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using MUnique.OpenMU.Network.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IShowMonsterAppearancePlugIn"/> which sends a
/// <see cref="Network.Packets.ServerToClient.MonsterAppearance"/> message to the game client.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.ShowMonsterAppearancePlugIn_Name), Description = nameof(PlugInResources.ShowMonsterAppearancePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("0708ED02-F593-4684-8651-90AA1F0B2325")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public class ShowMonsterAppearancePlugIn : IShowMonsterAppearancePlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowMonsterAppearancePlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ShowMonsterAppearancePlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask ShowMonsterAppearanceAsync(NonPlayerCharacter npc, GameLogic.NPC.MonsterAppearance appearance)
    {
        if (this._player.Connection is not { Connected: true } connection)
        {
            return;
        }

        var name = Utf8Text.Truncate(appearance.Name, GameLogic.NPC.MonsterAppearance.MaximumNameLength);
        var scalePercent = (ushort)Math.Clamp(appearance.ScalePercent, 1, ushort.MaxValue);
        await connection.SendMonsterAppearanceAsync((byte)appearance.Glow, npc.Id, scalePercent, name).ConfigureAwait(false);
    }
}
