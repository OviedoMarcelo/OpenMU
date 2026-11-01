// <copyright file="AddKundunCollectorPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Maps;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Adds the Coleccionista de Excelentes, an NPC which exchanges excellent items and jewels for a Box of Kundun, to Elvenland in existing databases.
/// New databases get it from the initialization, see <see cref="CustomMonstersInitialization"/>.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("7C2C8B7D-3E5A-4B0F-9B55-2B7E4A1D7102")]
public class AddKundunCollectorPlugIn : UpdatePlugInBase
{
    /// <summary>
    /// The plug-in name.
    /// </summary>
    internal const string PlugInName = "Add the Coleccionista de Excelentes";

    /// <summary>
    /// The plug-in description.
    /// </summary>
    internal const string PlugInDescription = "Adds the Coleccionista de Excelentes (710) to the town of Elvenland. It exchanges excellent items and a pack of Jewels of Creation for a Box of Kundun +5. The client needs to know the NPC.";

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => false;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        var initialization = new CustomMonstersInitialization(context, gameConfiguration);
        var npc = initialization.CreateKundunCollector();
        if (gameConfiguration.Maps.FirstOrDefault(m => m.Number == Elvenland.Number) is { } elvenland)
        {
            initialization.CreateKundunCollectorSpawn(elvenland, npc);
        }

        return ValueTask.CompletedTask;
    }
}
