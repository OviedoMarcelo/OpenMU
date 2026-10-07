// <copyright file="PlayerTitleViewPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlugIns.Achievements;
using MUnique.OpenMU.GameLogic.Views;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using MUnique.OpenMU.Network.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IPlayerTitleViewPlugIn"/> which sends a
/// <see cref="PlayerTitle"/> message to the game client.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.PlayerTitleViewPlugIn_Name), Description = nameof(PlugInResources.PlayerTitleViewPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("BF5582AE-8BDC-4F91-9B45-CB67699C74AB")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public class PlayerTitleViewPlugIn : IPlayerTitleViewPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlayerTitleViewPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public PlayerTitleViewPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask ShowTitleAsync(Player player, TitleDefinition? title)
    {
        if (this._player.Connection is not { Connected: true } connection)
        {
            return;
        }

        var text = title?.Text ?? string.Empty;
        if (text.Length > TitleDefinition.MaximumTextLength)
        {
            text = text[..TitleDefinition.MaximumTextLength];
        }

        await connection.SendPlayerTitleAsync(player.GetId(this._player), title?.GetArgb() ?? 0, text).ConfigureAwait(false);
    }
}
