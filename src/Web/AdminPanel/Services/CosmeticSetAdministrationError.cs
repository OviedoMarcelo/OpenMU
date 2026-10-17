// <copyright file="CosmeticSetAdministrationError.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.Services;

/// <summary>
/// The errors of the administration of the cosmetic sets.
/// </summary>
public enum CosmeticSetAdministrationError
{
    /// <summary>
    /// No error.
    /// </summary>
    None,

    /// <summary>
    /// The plugin of the cosmetic sets isn't active, or the storage isn't registered.
    /// </summary>
    PlugInInactive,

    /// <summary>
    /// There is no set with the specified number.
    /// </summary>
    SetNotFound,

    /// <summary>
    /// The character hasn't unlocked the set.
    /// </summary>
    NotUnlocked,

    /// <summary>
    /// The storage couldn't be reached.
    /// </summary>
    StorageUnavailable,
}
