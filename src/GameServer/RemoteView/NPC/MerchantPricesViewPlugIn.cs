// <copyright file="MerchantPricesViewPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.NPC;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.Views.NPC;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using MUnique.OpenMU.Network.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IMerchantPricesViewPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.MerchantPricesViewPlugIn_Name), Description = nameof(PlugInResources.MerchantPricesViewPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("6C1F4E2A-93B7-4D58-A0E6-2F7B9C3D8E15")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public class MerchantPricesViewPlugIn : IMerchantPricesViewPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="MerchantPricesViewPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public MerchantPricesViewPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public ValueTask ShowMerchantPricesAsync()
    {
        if (this._player.Connection is not { Connected: true } connection)
        {
            return ValueTask.CompletedTask;
        }

        // The count has to fit into the C2 message, which the client couldn't read otherwise.
        var maximumCount = (ushort.MaxValue - MerchantPricesRef.GetRequiredSize(0)) / MerchantPricesRef.MerchantPriceRef.Length;
        var prices = MerchantStorePrices.GetPrices(this._player.GameContext.Configuration, this._player.OpenedNpc?.Definition)
            .Where(p => p.Key.Definition.Number >= 0)
            .Take(maximumCount)
            .ToList();

        int Write()
        {
            var size = MerchantPricesRef.GetRequiredSize(prices.Count);
            var span = connection.Output.GetSpan(size)[..size];
            var packet = new MerchantPricesRef(span)
            {
                PriceCount = (ushort)prices.Count,
            };

            for (var i = 0; i < prices.Count; i++)
            {
                var price = prices[i];
                var target = packet[i];
                target.ItemGroup = price.Key.Definition.Group;
                target.ItemNumber = (ushort)price.Key.Definition.Number;
                target.ItemLevel = price.Key.Level;
                target.Price = (uint)Math.Clamp(price.Value.Price, 0, int.MaxValue);
                target.IsPricePerPiece = price.Value.IsPerPiece;
            }

            return size;
        }

        return connection.SendAsync(Write);
    }
}
