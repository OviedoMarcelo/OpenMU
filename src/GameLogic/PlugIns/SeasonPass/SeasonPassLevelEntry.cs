// <copyright file="SeasonPassLevelEntry.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.SeasonPass;

/// <summary>
/// A level of the season pass and whether an account received its rewards.
/// </summary>
/// <param name="Level">The level.</param>
/// <param name="IsFreeClaimed">If set to <c>true</c>, the rewards of the free track have been handed out.</param>
/// <param name="IsPremiumClaimed">If set to <c>true</c>, the rewards of the premium track have been handed out.</param>
public sealed record SeasonPassLevelEntry(SeasonLevelDefinition Level, bool IsFreeClaimed, bool IsPremiumClaimed);
