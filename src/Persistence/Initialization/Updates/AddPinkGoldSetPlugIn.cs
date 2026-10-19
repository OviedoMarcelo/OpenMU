// <copyright file="AddPinkGoldSetPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Adds the Pink Gold set (item number 54 of the helms, armors, pants, gloves and boots), which every class can wear.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("80A4B5D1-D5D3-41CD-81F6-6FD71085889A")]
public class AddPinkGoldSetPlugIn : AddSkinArmorSetPlugInBase
{
    /// <summary>
    /// The plug-in name.
    /// </summary>
    internal const string PlugInName = "Add the Pink Gold set";

    /// <summary>
    /// The plug-in description.
    /// </summary>
    internal const string PlugInDescription = "Adds the Pink Gold set, an armor set which every class can wear.";

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override short SetNumber => 54;

    /// <inheritdoc />
    protected override string[] PieceNames => ["Pink Gold Helm", "Pink Gold Armor", "Pink Gold Pants", "Pink Gold Gloves", "Pink Gold Boots"];
}
