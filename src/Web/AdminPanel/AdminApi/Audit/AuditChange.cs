// <copyright file="AuditChange.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AdminApi.Audit;

/// <summary>
/// A changed property of an <see cref="AuditEntry"/>.
/// </summary>
/// <param name="Property">The name of the property.</param>
/// <param name="Caption">The caption of the property.</param>
/// <param name="Before">The previous value as text, if it's a simple value.</param>
/// <param name="After">The new value as text, if it's a simple value.</param>
public record AuditChange(string Property, string Caption, string? Before, string? After);
