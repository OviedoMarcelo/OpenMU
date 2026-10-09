// <copyright file="ServerStatistics.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence;

/// <summary>
/// Aggregated numbers about the stored data.
/// </summary>
/// <param name="AccountCount">The number of accounts.</param>
/// <param name="CharacterCount">The number of characters.</param>
public record ServerStatistics(int AccountCount, int CharacterCount);
