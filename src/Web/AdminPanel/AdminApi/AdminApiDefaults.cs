// <copyright file="AdminApiDefaults.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AdminApi;

/// <summary>
/// Constants of the admin API, which is used by the separate admin frontend.
/// </summary>
public static class AdminApiDefaults
{
    /// <summary>
    /// The name of the authentication scheme of the admin API tokens.
    /// </summary>
    public const string AuthenticationScheme = "OpenMU.AdminToken";

    /// <summary>
    /// The route prefix of all admin API endpoints.
    /// </summary>
    public const string RoutePrefix = "api/admin";

    /// <summary>
    /// Gets the header which carries the token.
    /// </summary>
    public static string TokenHeaderName => "X-Admin-Token";
}
