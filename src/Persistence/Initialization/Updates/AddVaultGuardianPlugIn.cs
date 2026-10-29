// <copyright file="AddVaultGuardianPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Maps;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Adds the Guardián del Baúl, an NPC which extends the vault, next to the vault keeper of Lorencia in existing databases.
/// New databases get it from the initialization, see <see cref="CustomMonstersInitialization"/>.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("47118935-37D8-4F1B-AD8C-3FAAD7880C8A")]
public class AddVaultGuardianPlugIn : UpdatePlugInBase
{
    /// <summary>
    /// The plug-in name.
    /// </summary>
    internal const string PlugInName = "Add the Guardián del Baúl";

    /// <summary>
    /// The plug-in description.
    /// </summary>
    internal const string PlugInDescription = "Adds the Guardián del Baúl (708) next to the vault keeper of Lorencia. It extends the vault of the account in exchange for packs of jewels (see the Vault Extension NPC plugin). The client needs to know the NPC.";

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => false;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        var initialization = new CustomMonstersInitialization(context, gameConfiguration);
        var npc = initialization.CreateVaultGuardian();
        if (gameConfiguration.Maps.FirstOrDefault(m => m.Number == Lorencia.Number) is { } lorencia)
        {
            initialization.CreateVaultGuardianSpawn(lorencia, npc);
        }

        return ValueTask.CompletedTask;
    }
}
