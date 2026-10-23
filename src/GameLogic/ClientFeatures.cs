// <copyright file="ClientFeatures.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic;

using System.Runtime.CompilerServices;

/// <summary>
/// Optional features which a game client tells that it supports.
/// </summary>
[Flags]
public enum ClientFeature : byte
{
    /// <summary>
    /// No optional feature.
    /// </summary>
    None = 0,

    /// <summary>
    /// The client shows the dialog of an NPC whose talk is a step of a quest, and answers it.
    /// </summary>
    NpcQuestDialog = 1,
}

/// <summary>
/// Keeps the optional features which the client of a player supports.
/// </summary>
public static class ClientFeatures
{
    private static readonly ConditionalWeakTable<Player, StrongBox<ClientFeature>> Features = new();

    /// <summary>
    /// Sets the features which the client of the player supports.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="features">The features.</param>
    public static void Set(Player player, ClientFeature features)
    {
        Features.AddOrUpdate(player, new StrongBox<ClientFeature>(features));
    }

    /// <summary>
    /// Determines whether the client of the player supports the feature.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="feature">The feature.</param>
    /// <returns><c>true</c>, if the client told that it supports the feature.</returns>
    public static bool Has(Player player, ClientFeature feature)
    {
        return Features.TryGetValue(player, out var features) && (features.Value & feature) == feature;
    }
}
