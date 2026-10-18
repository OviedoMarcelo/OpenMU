// <copyright file="CharacterCosmetics.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.Services;

/// <summary>
/// The cosmetic pieces of a character.
/// </summary>
/// <param name="IsOnline">A value indicating whether the character is in the game, so that changes apply immediately.</param>
/// <param name="Pieces">The configured pieces.</param>
public record CharacterCosmetics(bool IsOnline, IReadOnlyList<CosmeticPieceItem> Pieces);
