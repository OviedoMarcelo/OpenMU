// <copyright file="CosmeticPieceEntry.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.Cosmetics;

/// <summary>
/// A configured cosmetic piece, as a character sees it.
/// </summary>
/// <param name="Definition">The definition of the piece.</param>
/// <param name="Name">The name which is shown: the configured one, or else the one of the item.</param>
/// <param name="IsUnlocked">If set to <c>true</c>, the character has unlocked the piece.</param>
/// <param name="CanWear">If set to <c>true</c>, the class of the character can wear the piece.</param>
/// <param name="IsWorn">If set to <c>true</c>, the character wears the piece.</param>
public sealed record CosmeticPieceEntry(CosmeticPieceDefinition Definition, string Name, bool IsUnlocked, bool CanWear, bool IsWorn);
