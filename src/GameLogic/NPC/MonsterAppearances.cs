// <copyright file="MonsterAppearances.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.NPC;

using System.Runtime.CompilerServices;

/// <summary>
/// The appearances of single monsters, which the client shows differently than the others of their kind.
/// </summary>
/// <remarks>
/// The appearance is sent to a player when the monster comes into its view, so it has to be set before
/// the monster is added to the map.
/// </remarks>
public static class MonsterAppearances
{
    private static readonly ConditionalWeakTable<NonPlayerCharacter, MonsterAppearance> Appearances = new();

    /// <summary>
    /// Gets the appearance of a monster.
    /// </summary>
    /// <param name="npc">The monster.</param>
    /// <returns>The appearance; <c>null</c>, if it looks like the others of its kind.</returns>
    public static MonsterAppearance? Get(NonPlayerCharacter npc)
    {
        return Appearances.TryGetValue(npc, out var appearance) ? appearance : null;
    }

    /// <summary>
    /// Sets the appearance of a monster.
    /// </summary>
    /// <param name="npc">The monster.</param>
    /// <param name="appearance">The appearance; <c>null</c> to look like the others of its kind.</param>
    public static void Set(NonPlayerCharacter npc, MonsterAppearance? appearance)
    {
        if (appearance is null || appearance.IsDefault)
        {
            Appearances.Remove(npc);
        }
        else
        {
            Appearances.AddOrUpdate(npc, appearance);
        }
    }
}
