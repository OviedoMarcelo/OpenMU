// <copyright file="AddChristmasEventPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Adds the data of the Christmas event of MU Cyber to existing databases.
/// New databases get it from the initialization, see <see cref="ChristmasEventInitialization"/>.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("B8D2F4A6-1E3C-4B5D-9A7F-3C5E7A9B1D2F")]
public class AddChristmasEventPlugIn : UpdatePlugInBase
{
    /// <summary>
    /// The plug-in name.
    /// </summary>
    internal const string PlugInName = "Add the Christmas event";

    /// <summary>
    /// The plug-in description.
    /// </summary>
    internal const string PlugInDescription = "Adds the Christmas event: Invitation to Santa Village (13,66) sold by the Duende de Santa (711) in Lorencia, Santa Claus and the arrival gate in Santa Village, the buffs of the Little Santas, the prizes of the Christmas Star and the monsters of the invasion of Santa. The client needs to know the NPC 711.";

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => false;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        new ChristmasEventInitialization(context, gameConfiguration).Initialize();
        return ValueTask.CompletedTask;
    }
}
