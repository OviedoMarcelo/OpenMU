// <copyright file="BossEncounterOutcome.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.BossEncounters;

using System.Text.Json.Serialization;

/// <summary>
/// How a boss encounter ended.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<BossEncounterOutcome>))]
public enum BossEncounterOutcome
{
    /// <summary>
    /// The boss was defeated.
    /// </summary>
    Defeated,

    /// <summary>
    /// The boss retreated, because the maximum duration was reached.
    /// </summary>
    Retreated,

    /// <summary>
    /// The encounter was ended, e.g. by a game master.
    /// </summary>
    Stopped,
}
