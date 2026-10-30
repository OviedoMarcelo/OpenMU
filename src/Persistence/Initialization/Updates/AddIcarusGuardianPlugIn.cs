// <copyright file="AddIcarusGuardianPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.Version095d.Maps;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Adds the Guardiana de Icarus, the strongest monster of Icarus, with its spawns and its drop of the Flame of Condor.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("3F5C8B27-91D4-4E6A-B7A2-6C1E0D9F4A53")]
public class AddIcarusGuardianPlugIn : UpdatePlugInBase
{
    /// <summary>
    /// The plug-in name.
    /// </summary>
    internal const string PlugInName = "Add the Guardiana de Icarus";

    /// <summary>
    /// The plug-in description.
    /// </summary>
    internal const string PlugInDescription = "Adds the Guardiana de Icarus (709), a monster of level 120 with 4 spawns in Icarus. It has a chance of 0.5% to drop the Flame of Condor, for players with 5 resets or more. The client needs to know the monster.";

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => false;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        var initialization = new CustomMonstersInitialization(context, gameConfiguration);
        var monster = initialization.CreateIcarusGuardian();
        if (gameConfiguration.Maps.FirstOrDefault(m => m.Number == Icarus.Number) is { } icarus)
        {
            initialization.CreateIcarusGuardianSpawns(icarus, monster);
        }

        return ValueTask.CompletedTask;
    }
}
