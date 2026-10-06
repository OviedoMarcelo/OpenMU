// <copyright file="IItemCraftedPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns;

using System.Runtime.InteropServices;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// A plugin interface which is called after a crafting (e.g. a chaos machine mix) has been finished.
/// </summary>
[Guid("D98F01DC-6F64-4B2A-A926-5D41142AB96E")]
[PlugInPoint("Item crafted", "Plugins which will be executed after a crafting (e.g. a chaos machine mix) has been finished, successfully or not.")]
public interface IItemCraftedPlugIn
{
    /// <summary>
    /// Is called after a crafting has been finished.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="success">If set to <c>true</c>, the crafting succeeded.</param>
    /// <param name="resultItem">The resulting item, e.g. an upgraded item or a new wing; <c>null</c>, if the crafting failed.</param>
    ValueTask ItemCraftedAsync(Player player, bool success, Item? resultItem);
}
