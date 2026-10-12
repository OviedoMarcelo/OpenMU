// <copyright file="BossEncounterAdministrationError.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.Services;

/// <summary>
/// Why an action on the boss encounters of a game server failed.
/// </summary>
public enum BossEncounterAdministrationError
{
    /// <summary>
    /// No error.
    /// </summary>
    None,

    /// <summary>
    /// The game server doesn't exist.
    /// </summary>
    GameServerUnavailable,

    /// <summary>
    /// The game server doesn't run in this process, so its encounters can't be reached.
    /// </summary>
    AllInOneDeploymentRequired,

    /// <summary>
    /// The plugin of the boss encounters isn't active.
    /// </summary>
    PlugInInactive,

    /// <summary>
    /// There is no encounter with the name.
    /// </summary>
    EncounterNotFound,

    /// <summary>
    /// The encounter is already running.
    /// </summary>
    AlreadyRunning,

    /// <summary>
    /// The encounter isn't running.
    /// </summary>
    NotRunning,
}
