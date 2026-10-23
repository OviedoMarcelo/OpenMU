// <copyright file="AddEncounterObjectsPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Adds the objects of the boss encounters (crystals, pillars and obelisks in ice and fire) to existing databases.
/// New databases get them from the initialization, see <see cref="CustomMonstersInitialization"/>.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("9F1C4A6E-2D83-4B57-8E0A-5C7B3D9F1A26")]
public class AddEncounterObjectsPlugIn : UpdatePlugInBase
{
    /// <summary>
    /// The plug-in name.
    /// </summary>
    internal const string PlugInName = "Add the objects of the boss encounters";

    /// <summary>
    /// The plug-in description.
    /// </summary>
    internal const string PlugInDescription = "Adds crystals (702, 703), rune pillars (704, 705) and obelisks (706, 707) in ice and fire, with their own models, for the phases of the boss encounters. They don't move, don't attack and drop nothing.";

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => false;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 22, 23, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        new CustomMonstersInitialization(context, gameConfiguration).CreateEncounterObjects();
        return ValueTask.CompletedTask;
    }
}
