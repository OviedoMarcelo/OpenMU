// <copyright file="IHealthRestoredPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns;

using System.Runtime.InteropServices;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// A plugin interface which is called when a player restored the health of an object with a skill, e.g. with the heal of an elf.
/// </summary>
[Guid("B6E1D3A4-7C2F-4E58-9A1D-5F0C8B3E2A71")]
[PlugInPoint("Health restored", "Plugins which will be executed when a player restored the health of an object with a skill.")]
public interface IHealthRestoredPlugIn
{
    /// <summary>
    /// This method is called when a player restored the health of an object with a skill.
    /// </summary>
    /// <param name="target">The object whose health was restored.</param>
    /// <param name="healer">The player who used the skill.</param>
    /// <param name="amount">The restored health, without the part which exceeded the maximum health.</param>
    void HealthRestored(IAttackable target, Player healer, float amount);
}
