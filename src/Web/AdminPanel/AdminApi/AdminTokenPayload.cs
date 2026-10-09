// <copyright file="AdminTokenPayload.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AdminApi;

/// <summary>
/// The payload of an admin API token.
/// </summary>
/// <param name="UserId">The id of the admin user.</param>
/// <param name="SecurityStamp">The security stamp of the user at the time of the login.</param>
/// <param name="UsedSecondFactor">If set to <c>true</c>, the user authenticated with a second factor.</param>
public record AdminTokenPayload(Guid UserId, string SecurityStamp, bool UsedSecondFactor);
