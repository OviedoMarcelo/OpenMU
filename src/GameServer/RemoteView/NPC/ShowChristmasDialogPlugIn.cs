// <copyright file="ShowChristmasDialogPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.NPC;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.Views.NPC;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IShowChristmasDialogPlugIn"/>, which sends a "server command"
/// (C1 F3 40) to the client. The client shows its dialogs of Santa Village for the types 16 and 17:
/// Santa Claus (16, with 1 = "please come and claim your gift") and the Snowman (17), see ReceiveServerCommand of MuMain.
/// </summary>
[PlugIn]
[Display(Name = "Show Christmas Dialog", Description = "Muestra en el cliente los diálogos de Santa Claus y del Snowman de Santa Village.")]
[Guid("6B1E4D2C-8A3F-4C5B-9E7D-1A2B3C4D5E6F")]
public class ShowChristmasDialogPlugIn : IShowChristmasDialogPlugIn
{
    /// <summary>
    /// The type of the server command for the dialog of Santa Claus.
    /// </summary>
    private const byte SantaClausCommand = 16;

    /// <summary>
    /// The parameter of <see cref="SantaClausCommand"/> for "Welcome to Santa's Village, please come and claim your gift".
    /// </summary>
    private const byte ClaimYourGift = 1;

    /// <summary>
    /// The type of the server command for the dialog of the Snowman.
    /// </summary>
    private const byte SnowmanCommand = 17;

    /// <summary>
    /// The type of the server command of MU Cyber which tells the client whether it shows the Christmas version of
    /// Lorencia (1) or the normal one (0), see ReceiveServerCommand of MuMain.
    /// </summary>
    private const byte ChristmasLorenciaCommand = 200;

    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowChristmasDialogPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ShowChristmasDialogPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public async ValueTask ShowSantaClausDialogAsync()
    {
        // The packet of the fireworks is the same server command with other values: the type, then two parameters.
        await this._player.Connection.SendShowChristmasFireworksAsync(ClaimYourGift, 0, SantaClausCommand).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask ShowSnowmanDialogAsync()
    {
        await this._player.Connection.SendShowChristmasFireworksAsync(0, 0, SnowmanCommand).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask SetChristmasLorenciaAsync(bool enabled)
    {
        await this._player.Connection.SendShowChristmasFireworksAsync(enabled ? (byte)1 : (byte)0, 0, ChristmasLorenciaCommand).ConfigureAwait(false);
    }
}
