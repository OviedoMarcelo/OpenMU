// <copyright file="AddJapaneseWhiteSetPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Adds the Japanese White set (item number 55 of the helms, armors, pants, gloves and boots), which every class can wear.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("1421CB0D-E1DB-4CBF-A038-AD5CB2E34C81")]
public class AddJapaneseWhiteSetPlugIn : AddSkinArmorSetPlugInBase
{
    /// <summary>
    /// The plug-in name.
    /// </summary>
    internal const string PlugInName = "Add the Japanese White set";

    /// <summary>
    /// The plug-in description.
    /// </summary>
    internal const string PlugInDescription = "Adds the Japanese White set, an armor set which every class can wear.";

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 19, 18, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override short SetNumber => 55;

    /// <inheritdoc />
    protected override string[] PieceNames => ["Japanese White Helm", "Japanese White Armor", "Japanese White Pants", "Japanese White Gloves", "Japanese White Boots"];
}
