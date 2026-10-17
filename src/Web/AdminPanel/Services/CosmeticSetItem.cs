// <copyright file="CosmeticSetItem.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.Services;

/// <summary>
/// A configured cosmetic set, as a character has it.
/// </summary>
/// <param name="Number">The number of the set.</param>
/// <param name="Name">The name of the set.</param>
/// <param name="ItemNumber">The number of the items of the armor set.</param>
/// <param name="Level">The level which the pieces show.</param>
/// <param name="IsExcellent">A value indicating whether the pieces glow like excellent items.</param>
/// <param name="IsUnlocked">A value indicating whether the character unlocked the set.</param>
/// <param name="IsActive">A value indicating whether the character wears the set.</param>
public record CosmeticSetItem(short Number, string Name, short ItemNumber, byte Level, bool IsExcellent, bool IsUnlocked, bool IsActive);
