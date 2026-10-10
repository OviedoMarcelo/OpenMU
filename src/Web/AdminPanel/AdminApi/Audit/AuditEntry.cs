// <copyright file="AuditEntry.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AdminApi.Audit;

/// <summary>
/// A change which an admin made through the admin API.
/// </summary>
/// <param name="Timestamp">The time of the change.</param>
/// <param name="User">The login name of the admin, or <c>null</c> in the initial setup mode.</param>
/// <param name="Action">The action.</param>
/// <param name="Type">The name of the changed type.</param>
/// <param name="TypeCaption">The caption of the changed type.</param>
/// <param name="ObjectId">The id of the changed object.</param>
/// <param name="ObjectName">The name of the changed object.</param>
/// <param name="Changes">The changed properties; empty when an object was deleted.</param>
public record AuditEntry(
    DateTimeOffset Timestamp,
    string? User,
    AuditAction Action,
    string Type,
    string TypeCaption,
    Guid ObjectId,
    string ObjectName,
    IReadOnlyList<AuditChange> Changes);
