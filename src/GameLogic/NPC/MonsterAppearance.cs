// <copyright file="MonsterAppearance.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.NPC;

/// <summary>
/// How the client shows a single monster differently than the others of its kind, e.g. a boss.
/// </summary>
/// <param name="ScalePercent">The size in percent of the normal size of the monster.</param>
/// <param name="Glow">The glow.</param>
/// <param name="Name">The name which is shown instead of the name of the monster; empty for its own name.</param>
public sealed record MonsterAppearance(int ScalePercent, MonsterGlow Glow, string Name)
{
    /// <summary>
    /// The maximum length of the <see cref="Name"/> in bytes of UTF-8, as it's sent to the client.
    /// </summary>
    public const int MaximumNameLength = 32;

    /// <summary>
    /// Gets a value indicating whether the monster looks like the others of its kind.
    /// </summary>
    public bool IsDefault => this.ScalePercent == 100 && this.Glow == MonsterGlow.None && string.IsNullOrEmpty(this.Name);
}
