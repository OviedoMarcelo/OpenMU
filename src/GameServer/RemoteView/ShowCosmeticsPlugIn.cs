// <copyright file="ShowCosmeticsPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlugIns.Cosmetics;
using MUnique.OpenMU.GameLogic.Views;
using MUnique.OpenMU.GameServer.RemoteView.World;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using MUnique.OpenMU.Network.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IShowCosmeticsPlugIn"/> which sends a
/// <see cref="CosmeticPieces"/> message to the game client.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.ShowCosmeticsPlugIn_Name), Description = nameof(PlugInResources.ShowCosmeticsPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("8F3C2D61-4B7A-4E0D-A1C5-2E9B6D04F713")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public class ShowCosmeticsPlugIn : IShowCosmeticsPlugIn
{
    /// <summary>
    /// The maximum number of pieces which the message holds.
    /// </summary>
    private const int MaximumPieceCount = 1000;

    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowCosmeticsPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ShowCosmeticsPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public ValueTask ShowCosmeticsAsync(IReadOnlyList<CosmeticPieceEntry> pieces)
    {
        if (this._player.Connection is not { Connected: true } connection)
        {
            return ValueTask.CompletedTask;
        }

        var entries = pieces.Take(MaximumPieceCount).ToList();

        int Write()
        {
            var size = CosmeticPiecesRef.GetRequiredSize(entries.Count);
            var span = connection.Output.GetSpan(size)[..size];
            var packet = new CosmeticPiecesRef(span)
            {
                PieceCount = (ushort)entries.Count,
            };

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var target = packet[i];
                target.Number = (ushort)entry.Definition.Number;
                target.ItemNumber = (ushort)entry.Definition.ItemNumber;
                target.ItemGroup = entry.Definition.Slot.GetItemGroup();
                target.Level = entry.Definition.Level;
                target.IsExcellent = entry.Definition.IsExcellent;
                target.IsUnlocked = entry.IsUnlocked;
                target.CanWear = entry.CanWear;
                target.IsWorn = entry.IsWorn;
                target.Name = Utf8Text.Truncate(entry.Name, CosmeticPieceDefinition.MaximumNameLength);
            }

            return size;
        }

        return connection.SendAsync(Write);
    }
}
