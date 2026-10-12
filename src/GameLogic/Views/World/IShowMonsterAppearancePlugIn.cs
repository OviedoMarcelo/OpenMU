// <copyright file="IShowMonsterAppearancePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Views.World;

using MUnique.OpenMU.GameLogic.NPC;

/// <summary>
/// Interface of a view whose client can show a single monster differently than the others of its kind, e.g. a boss.
/// </summary>
public interface IShowMonsterAppearancePlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows the appearance of a monster which is in view.
    /// </summary>
    /// <param name="npc">The monster.</param>
    /// <param name="appearance">The appearance.</param>
    ValueTask ShowMonsterAppearanceAsync(NonPlayerCharacter npc, MonsterAppearance appearance);
}
