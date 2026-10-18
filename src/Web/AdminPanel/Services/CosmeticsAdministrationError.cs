// <copyright file="CosmeticsAdministrationError.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.Services;

/// <summary>
/// The errors of the administration of the cosmetic pieces.
/// </summary>
public enum CosmeticsAdministrationError
{
    /// <summary>
    /// No error.
    /// </summary>
    None,

    /// <summary>
    /// The plugin of the cosmetic pieces isn't active, or the storage isn't registered.
    /// </summary>
    PlugInInactive,

    /// <summary>
    /// There is no piece with the specified number.
    /// </summary>
    PieceNotFound,

    /// <summary>
    /// The character hasn't unlocked the piece.
    /// </summary>
    NotUnlocked,

    /// <summary>
    /// The storage couldn't be reached.
    /// </summary>
    StorageUnavailable,
}
