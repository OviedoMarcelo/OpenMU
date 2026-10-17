// <copyright file="CosmeticSetEntry.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.CosmeticSets;

/// <summary>
/// A configured cosmetic set, as a character sees it.
/// </summary>
/// <param name="Definition">The definition of the set.</param>
/// <param name="IsUnlocked">If set to <c>true</c>, the character has unlocked the set.</param>
/// <param name="CanWear">If set to <c>true</c>, the class of the character can wear the set.</param>
public sealed record CosmeticSetEntry(CosmeticSetDefinition Definition, bool IsUnlocked, bool CanWear);
