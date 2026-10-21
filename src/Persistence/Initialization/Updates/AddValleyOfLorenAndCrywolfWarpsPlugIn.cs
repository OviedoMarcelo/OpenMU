// <copyright file="AddValleyOfLorenAndCrywolfWarpsPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Adds Valley of Loren and Crywolf to the move window (M), which were only reachable by walking from Lorencia,
/// and moves Vulcanus to the index with which the client asks for it (42 instead of 37).
/// </summary>
/// <remarks>
/// The client shows the destinations of its MoveReq file and sends their index, so the indexes, levels and costs
/// here are the same as there.
/// </remarks>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("8E3B6D41-2C7A-4F95-B1E8-6A0D3C9F2B57")]
public class AddValleyOfLorenAndCrywolfWarpsPlugIn : UpdatePlugInBase
{
    /// <summary>
    /// The plug-in name.
    /// </summary>
    internal const string PlugInName = "Add the Valley of Loren and Crywolf warps";

    /// <summary>
    /// The plug-in description.
    /// </summary>
    internal const string PlugInDescription = "Adds Valley of Loren and Crywolf to the move window (M), and moves Vulcanus to the index which the client uses (42).";

    private const ushort OldVulcanusIndex = 37;

    private const ushort VulcanusIndex = 42;

    /// <summary>
    /// The warps to add: index, name, costs, level requirement, and the map and the corner of the exit gate where
    /// the players arrive when they walk there (from Lorencia, and from Valley of Loren).
    /// </summary>
    private static readonly (ushort Index, string Name, int Costs, int Level, short Map, byte X1, byte Y1)[] Warps =
    [
        (49, "ValleyOfLoren", 10000, 10, 30, 29, 37),
        (50, "Crywolf", 15000, 150, 34, 231, 37),
    ];

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => false;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 21, 18, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        foreach (var (index, name, costs, level, mapNumber, x1, y1) in Warps)
        {
            if (gameConfiguration.WarpList.Any(w => w.Index == index))
            {
                continue;
            }

            var gate = gameConfiguration.Maps
                .FirstOrDefault(m => m.Number == mapNumber)?
                .ExitGates.FirstOrDefault(g => g.X1 == x1 && g.Y1 == y1);
            if (gate is null)
            {
                continue;
            }

            var warp = context.CreateNew<WarpInfo>();
            warp.Index = index;
            warp.Name = name;
            warp.Costs = costs;
            warp.LevelRequirement = level;
            warp.Gate = gate;
            gameConfiguration.WarpList.Add(warp);
        }

        if (gameConfiguration.WarpList.All(w => w.Index != VulcanusIndex)
            && gameConfiguration.WarpList.FirstOrDefault(w => w.Index == OldVulcanusIndex && w.Name.Value == "Vulcanus") is { } vulcanus)
        {
            vulcanus.Index = VulcanusIndex;
        }

        return default;
    }
}
