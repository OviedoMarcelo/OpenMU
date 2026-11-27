// <copyright file="EconomyFeaturePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.Economy;

using System.Runtime.InteropServices;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Holds the settings of the economy of MU Cyber: how much zen the monsters drop, independent of the experience
/// rate, and how much less a player gets while the MU Helper or the offline leveling plays for them.
/// </summary>
[PlugIn]
[Display(Name = "Economy", Description = "Economía: el zen que tiran los monstruos (aparte del rate de experiencia) y cuánto menos zen y joyas reciben los que juegan con el MU Helper o el offlevel.")]
[Guid("D9C6BCD4-3CF3-453F-9ABA-59CF4CBB4DF4")]
public class EconomyFeaturePlugIn : IFeaturePlugIn, ISupportCustomConfiguration<EconomyConfiguration>, ISupportDefaultCustomConfiguration
{
    /// <inheritdoc/>
    public EconomyConfiguration? Configuration { get; set; }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new EconomyConfiguration();
}
