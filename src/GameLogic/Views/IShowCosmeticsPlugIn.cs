// <copyright file="IShowCosmeticsPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Views;

using MUnique.OpenMU.GameLogic.PlugIns.Cosmetics;

/// <summary>
/// Interface of a view whose client can show the cosmetic pieces of its own character,
/// and wear the worn ones over the armor of its character.
/// </summary>
public interface IShowCosmeticsPlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows the cosmetic pieces of the character of the player.
    /// </summary>
    /// <param name="pieces">The configured pieces, with the information whether the character unlocked and wears them.</param>
    ValueTask ShowCosmeticsAsync(IReadOnlyList<CosmeticPieceEntry> pieces);
}
