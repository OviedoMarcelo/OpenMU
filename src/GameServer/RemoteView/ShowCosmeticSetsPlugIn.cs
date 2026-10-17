// <copyright file="ShowCosmeticSetsPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlugIns.CosmeticSets;
using MUnique.OpenMU.GameLogic.Views;
using MUnique.OpenMU.GameServer.RemoteView.World;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using MUnique.OpenMU.Network.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IShowCosmeticSetsPlugIn"/> which sends a
/// <see cref="CosmeticSets"/> message to the game client.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.ShowCosmeticSetsPlugIn_Name), Description = nameof(PlugInResources.ShowCosmeticSetsPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("8F3C2D61-4B7A-4E0D-A1C5-2E9B6D04F713")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public class ShowCosmeticSetsPlugIn : IShowCosmeticSetsPlugIn
{
    /// <summary>
    /// The maximum number of sets which fit into the message.
    /// </summary>
    private const int MaximumSetCount = byte.MaxValue;

    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowCosmeticSetsPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ShowCosmeticSetsPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public ValueTask ShowCosmeticSetsAsync(IReadOnlyList<CosmeticSetEntry> sets, CosmeticSetDefinition? activeSet)
    {
        if (this._player.Connection is not { Connected: true } connection)
        {
            return ValueTask.CompletedTask;
        }

        var entries = sets.Take(MaximumSetCount).ToList();

        int Write()
        {
            var size = CosmeticSetsRef.GetRequiredSize(entries.Count);
            var span = connection.Output.GetSpan(size)[..size];
            var packet = new CosmeticSetsRef(span)
            {
                SetCount = (byte)entries.Count,
                ActiveSetNumber = (ushort)(activeSet?.Number ?? 0),
            };

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var target = packet[i];
                target.Number = (ushort)entry.Definition.Number;
                target.ItemNumber = (ushort)entry.Definition.ItemNumber;
                target.Level = entry.Definition.Level;
                target.IsExcellent = entry.Definition.IsExcellent;
                target.IsUnlocked = entry.IsUnlocked;
                target.CanWear = entry.CanWear;
                target.Name = Utf8Text.Truncate(entry.Definition.Name, CosmeticSetDefinition.MaximumNameLength);
            }

            return size;
        }

        return connection.SendAsync(Write);
    }
}
