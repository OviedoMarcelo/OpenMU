// <copyright file="CashShopRepositoryRegistry.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.CashShop;

/// <summary>
/// Holds the <see cref="ICashShopRepository"/> of the running server.
/// The plugins are created by the plugin manager and can't get it injected, so the host sets it here.
/// </summary>
public static class CashShopRepositoryRegistry
{
    /// <summary>
    /// Gets or sets the current repository. <c>null</c> when there is no database, e.g. in the demo mode.
    /// </summary>
    public static ICashShopRepository? Current { get; set; }
}
