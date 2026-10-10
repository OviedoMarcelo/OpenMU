// <copyright file="GameMasterPresenceConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns;

using MUnique.OpenMU.Interfaces;

/// <summary>
/// The configuration of the <see cref="GameMasterPresencePlugIn"/>.
/// </summary>
public class GameMasterPresenceConfiguration
{
    /// <summary>
    /// Gets or sets the message for all players when a game master enters the game.
    /// </summary>
    [Display(Name = "Online Message", Description = "Mensaje para todos cuando un GM entra al juego. {0} es el nombre del personaje. Vacío: no avisa.")]
    public string OnlineMessage { get; set; } = "El GM {0} está en línea. ¡Si necesitás ayuda, escribile!";

    /// <summary>
    /// Gets or sets how the online message is shown.
    /// </summary>
    [Display(Name = "Online Message Type", Description = "Cómo se ve el aviso de entrada: GoldenCenter (dorado, en el centro), BlueNormal (azul, en el chat) o SlideNotice.")]
    public MessageType OnlineMessageType { get; set; } = MessageType.GoldenCenter;

    /// <summary>
    /// Gets or sets the message for all players when a game master leaves the game.
    /// </summary>
    [Display(Name = "Offline Message", Description = "Mensaje para todos cuando un GM sale del juego o vuelve a la selección de personaje. {0} es el nombre del personaje. Vacío: no avisa.")]
    public string OfflineMessage { get; set; } = "El GM {0} se desconectó.";

    /// <summary>
    /// Gets or sets how the offline message is shown.
    /// </summary>
    [Display(Name = "Offline Message Type", Description = "Cómo se ve el aviso de salida: BlueNormal (azul, en el chat), GoldenCenter o SlideNotice.")]
    public MessageType OfflineMessageType { get; set; } = MessageType.BlueNormal;
}
