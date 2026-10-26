// <copyright file="AccountClaimResult.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic;

/// <summary>
/// The result of <see cref="AccountSessionRegistry.ClaimAsync"/>.
/// </summary>
public enum AccountClaimResult
{
    /// <summary>
    /// The account is claimed by the requesting player now.
    /// </summary>
    Claimed,

    /// <summary>
    /// The account is used by a template player, which doesn't persist anything,
    /// so it can be used by more than one player at the same time.
    /// </summary>
    Shared,

    /// <summary>
    /// Another player which is still in the game holds the account.
    /// </summary>
    InUse,

    /// <summary>
    /// The previous player of the account is still disconnecting (saving) and didn't finish in time.
    /// </summary>
    TeardownTimedOut,
}
