// <copyright file="ShowBossHealthPlugIn.cs" company="MUnique">
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
/// The default implementation of the <see cref="IShowBossHealthPlugIn"/> which sends a
/// <see cref="Network.Packets.ServerToClient.BossHealth"/> message to the game client.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.ShowBossHealthPlugIn_Name), Description = nameof(PlugInResources.ShowBossHealthPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("3E2B7C51-8A4D-4F0B-9C6E-1D5A2F8B7E40")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public class ShowBossHealthPlugIn : IShowBossHealthPlugIn
{
    private const byte HiddenState = 0;
    private const byte ShownState = 1;
    private const int MaximumNameLength = 32;

    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowBossHealthPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ShowBossHealthPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask ShowBossHealthAsync(NonPlayerCharacter boss, string bossName, string phaseName, byte healthPercent)
    {
        if (this._player.Connection is not { Connected: true } connection)
        {
            return;
        }

        await connection.SendBossHealthAsync(
            ShownState,
            boss.Id,
            Math.Min(healthPercent, (byte)100),
            Utf8Text.Truncate(bossName, MaximumNameLength),
            Utf8Text.Truncate(phaseName, MaximumNameLength)).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask HideBossHealthAsync(NonPlayerCharacter boss)
    {
        if (this._player.Connection is not { Connected: true } connection)
        {
            return;
        }

        await connection.SendBossHealthAsync(HiddenState, boss.Id, 0, string.Empty, string.Empty).ConfigureAwait(false);
    }
}
