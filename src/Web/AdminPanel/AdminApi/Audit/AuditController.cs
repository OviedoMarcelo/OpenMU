// <copyright file="AuditController.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AdminApi.Audit;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MUnique.OpenMU.Web.AdminPanel.Auth;

/// <summary>
/// Provides the changes which admins made through the admin API.
/// </summary>
[ApiController]
[Route(AdminApiDefaults.RoutePrefix + "/audit")]
[Authorize(AuthenticationSchemes = AdminApiDefaults.AuthenticationScheme, Policy = AdminPolicies.Viewer)]
public class AuditController : ControllerBase
{
    private const int MaximumLimit = 200;

    private readonly AdminAuditLog _auditLog;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuditController"/> class.
    /// </summary>
    /// <param name="auditLog">The audit log.</param>
    public AuditController(AdminAuditLog auditLog)
    {
        this._auditLog = auditLog;
    }

    /// <summary>
    /// Gets the latest changes, newest first.
    /// </summary>
    /// <param name="limit">The maximum number of changes.</param>
    /// <param name="objectId">The id of an object, to only get its changes.</param>
    /// <returns>The changes.</returns>
    [HttpGet]
    public Task<IReadOnlyList<AuditEntry>> GetLatestAsync([FromQuery] int limit = 20, [FromQuery] Guid? objectId = null)
    {
        return this._auditLog.GetLatestAsync(Math.Clamp(limit, 1, MaximumLimit), objectId);
    }
}
