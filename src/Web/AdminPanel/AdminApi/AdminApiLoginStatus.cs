// <copyright file="AdminApiLoginStatus.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AdminApi;

using System.Text.Json.Serialization;

/// <summary>
/// The status of a login attempt.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<AdminApiLoginStatus>))]
public enum AdminApiLoginStatus
{
    /// <summary>
    /// The credentials were wrong or the user is not allowed to log in.
    /// </summary>
    Failed,

    /// <summary>
    /// The user is locked out because of too many failed attempts.
    /// </summary>
    LockedOut,

    /// <summary>
    /// The password was correct, but the code of the second factor is required.
    /// </summary>
    TwoFactorRequired,

    /// <summary>
    /// A second factor is required by the configuration, but the user hasn't set one up yet.
    /// </summary>
    TwoFactorSetupRequired,

    /// <summary>
    /// The login succeeded.
    /// </summary>
    Succeeded,
}
