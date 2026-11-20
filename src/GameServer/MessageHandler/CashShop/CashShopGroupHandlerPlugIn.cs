// <copyright file="CashShopGroupHandlerPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.MessageHandler.CashShop;

using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.Network.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Packet handler for the messages of the cash shop (0xD2 identifier).
/// </summary>
[PlugIn]
[Display(Name = "Cash Shop Group Handler", Description = "Recibe los mensajes de la tienda del juego (0xD2).")]
[Guid("A5D3F0C8-1E62-4B7A-8C94-5F2E6B1D0A39")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
internal class CashShopGroupHandlerPlugIn : GroupPacketHandlerPlugIn
{
    /// <summary>
    /// The group key, the code of the messages of the cash shop.
    /// </summary>
    internal const byte GroupKey = 0xD2;

    /// <summary>
    /// Initializes a new instance of the <see cref="CashShopGroupHandlerPlugIn" /> class.
    /// </summary>
    /// <param name="clientVersionProvider">The client version provider.</param>
    /// <param name="manager">The manager.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    public CashShopGroupHandlerPlugIn(IClientVersionProvider clientVersionProvider, PlugInManager manager, ILoggerFactory loggerFactory)
        : base(clientVersionProvider, manager, loggerFactory)
    {
    }

    /// <inheritdoc/>
    public override bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public override byte Key => GroupKey;
}
