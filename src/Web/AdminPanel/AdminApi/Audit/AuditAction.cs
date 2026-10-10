// <copyright file="AuditAction.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AdminApi.Audit;

using System.Text.Json.Serialization;

/// <summary>
/// The action of an <see cref="AuditEntry"/>.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<AuditAction>))]
public enum AuditAction
{
    /// <summary>
    /// An object was created.
    /// </summary>
    Created,

    /// <summary>
    /// An object was changed.
    /// </summary>
    Updated,

    /// <summary>
    /// An object was deleted.
    /// </summary>
    Deleted,
}
