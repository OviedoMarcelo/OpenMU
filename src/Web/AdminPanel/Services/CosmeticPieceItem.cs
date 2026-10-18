// <copyright file="CosmeticPieceItem.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.Services;

/// <summary>
/// A configured cosmetic piece, as a character has it.
/// </summary>
/// <param name="Number">The number of the piece.</param>
/// <param name="Name">The name of the piece.</param>
/// <param name="Slot">The slot which the piece covers, e.g. "Helm".</param>
/// <param name="ItemGroup">The group of the item.</param>
/// <param name="ItemNumber">The number of the item in its group.</param>
/// <param name="Level">The level which the piece shows.</param>
/// <param name="IsExcellent">A value indicating whether the piece glows like an excellent item.</param>
/// <param name="IsUnlocked">A value indicating whether the character unlocked the piece.</param>
/// <param name="IsWorn">A value indicating whether the character wears the piece.</param>
public record CosmeticPieceItem(short Number, string Name, string Slot, byte ItemGroup, short ItemNumber, byte Level, bool IsExcellent, bool IsUnlocked, bool IsWorn);
