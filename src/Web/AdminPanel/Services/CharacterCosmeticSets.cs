// <copyright file="CharacterCosmeticSets.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.Services;

/// <summary>
/// The cosmetic sets of a character.
/// </summary>
/// <param name="IsOnline">A value indicating whether the character is in the game, so that changes apply immediately.</param>
/// <param name="Sets">The configured sets.</param>
public record CharacterCosmeticSets(bool IsOnline, IReadOnlyList<CosmeticSetItem> Sets);
