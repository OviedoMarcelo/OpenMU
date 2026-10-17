// <copyright file="IShowCosmeticSetsPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Views;

using MUnique.OpenMU.GameLogic.PlugIns.CosmeticSets;

/// <summary>
/// Interface of a view whose client can show the cosmetic sets of its own character,
/// and wear the active one over the armor of its character.
/// </summary>
public interface IShowCosmeticSetsPlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows the cosmetic sets of the character of the player.
    /// </summary>
    /// <param name="sets">The configured sets, with the information whether the character unlocked them.</param>
    /// <param name="activeSet">The set which the character wears; <c>null</c>, if it wears none.</param>
    ValueTask ShowCosmeticSetsAsync(IReadOnlyList<CosmeticSetEntry> sets, CosmeticSetDefinition? activeSet);
}
