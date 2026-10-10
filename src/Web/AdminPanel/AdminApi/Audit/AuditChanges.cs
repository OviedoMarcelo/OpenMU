// <copyright file="AuditChanges.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AdminApi.Audit;

using System.Text.Json.Nodes;
using MUnique.OpenMU.Web.AdminPanel.AdminApi.Configuration;

/// <summary>
/// Determines the changes of an object for the <see cref="AdminAuditLog"/>.
/// </summary>
public static class AuditChanges
{
    private const int MaximumValueLength = 200;

    /// <summary>
    /// Gets the properties whose values differ between two serializations of an object.
    /// </summary>
    /// <param name="schema">The schema of the object.</param>
    /// <param name="before">The object before the change, as serialized by the <see cref="ConfigurationValueSerializer"/>.</param>
    /// <param name="after">The object after the change.</param>
    /// <returns>The changes.</returns>
    public static IReadOnlyList<AuditChange> Between(TypeSchema schema, JsonObject before, JsonObject after)
    {
        var beforeValues = before["values"]!.AsObject();
        var afterValues = after["values"]!.AsObject();
        return schema.Properties
            .Where(p => !JsonNode.DeepEquals(beforeValues[p.Name], afterValues[p.Name]))
            .Select(p => new AuditChange(
                p.Name,
                p.Caption,
                Truncate(Describe(beforeValues[p.Name], p)),
                Truncate(Describe(afterValues[p.Name], p))))
            .ToList();
    }

    private static string? Truncate(string? value) =>
        value is { Length: > MaximumValueLength } ? value[..MaximumValueLength] + "…" : value;

    private static string? Describe(JsonNode? value, PropertySchema property)
    {
        return value switch
        {
            null => null,
            JsonObject reference when property.Kind is PropertyKind.Reference || (property.Kind is PropertyKind.Embedded && property.TargetIsBrowsable) => reference["name"]?.GetValue<string>(),
            JsonArray flags when property.Kind is PropertyKind.Flags => string.Join(", ", flags.Select(f => f?.GetValue<string>())),
            JsonArray list => $"{list.Count} elemento(s)",
            JsonObject => null,
            JsonValue simple when simple.TryGetValue<string>(out var text) => text,
            _ => value.ToJsonString(),
        };
    }
}
