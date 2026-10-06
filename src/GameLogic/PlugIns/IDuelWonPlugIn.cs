// <copyright file="IDuelWonPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns;

using System.Runtime.InteropServices;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// A plugin interface which is called when a duel has been finished with a winner.
/// </summary>
[Guid("8E7094A2-E3EA-46BF-ACDC-F60871C8ACA7")]
[PlugInPoint("Duel won", "Plugins which will be executed when a duel has been finished with a winner.")]
public interface IDuelWonPlugIn
{
    /// <summary>
    /// Is called when a duel has been finished with a winner.
    /// </summary>
    /// <param name="winner">The winner of the duel.</param>
    /// <param name="loser">The loser of the duel.</param>
    ValueTask DuelWonAsync(Player winner, Player loser);
}
