// <copyright file="AchievementOverviewEntry.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.Achievements;

/// <summary>
/// An achievement and the progress of a player in it.
/// </summary>
/// <param name="Achievement">The achievement.</param>
/// <param name="Count">The achieved count.</param>
/// <param name="Required">The required count.</param>
/// <param name="IsCompleted">If set to <c>true</c>, the achievement has been completed.</param>
/// <param name="IsRewarded">If set to <c>true</c>, the rewards have been handed out.</param>
public sealed record AchievementOverviewEntry(AchievementDefinition Achievement, long Count, int Required, bool IsCompleted, bool IsRewarded);
