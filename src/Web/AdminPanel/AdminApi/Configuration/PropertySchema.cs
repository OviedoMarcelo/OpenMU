// <copyright file="PropertySchema.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AdminApi.Configuration;

using System.Reflection;
using System.Text.Json.Serialization;

/// <summary>
/// Describes a property of a configuration type for the admin frontend.
/// </summary>
/// <param name="Name">The name of the property.</param>
/// <param name="Caption">The caption.</param>
/// <param name="Description">The description.</param>
/// <param name="Help">The help in Spanish, if any.</param>
/// <param name="Kind">The kind, which decides how the value is shown and edited.</param>
/// <param name="Group">The group of the property, if any.</param>
/// <param name="Order">The order of the property, if defined.</param>
/// <param name="IsNullable">If set to <c>true</c>, the value can be empty.</param>
/// <param name="ValueType">The .NET type of a number or of the values of a <see cref="PropertyKind.ValueList"/>, e.g. <c>Int32</c>.</param>
/// <param name="Minimum">The minimum of a number, if any.</param>
/// <param name="Maximum">The maximum of a number, if any.</param>
/// <param name="EnumValues">The possible values of an enumeration.</param>
/// <param name="TargetType">The type of a referenced or embedded object, or of the objects of a list.</param>
/// <param name="TargetIsBrowsable">If set to <c>true</c>, the <paramref name="TargetType"/> has its own list, so the objects are linked instead of shown inline.</param>
/// <param name="IsReadOnly">If set to <c>true</c>, the value can't be changed by the admin API.</param>
public record PropertySchema(
    string Name,
    string Caption,
    string? Description,
    HelpText? Help,
    PropertyKind Kind,
    string? Group,
    int? Order,
    bool IsNullable,
    string? ValueType,
    double? Minimum,
    double? Maximum,
    IReadOnlyList<EnumValue>? EnumValues,
    string? TargetType,
    bool TargetIsBrowsable,
    bool IsReadOnly)
{
    /// <summary>
    /// Gets the property which is described.
    /// </summary>
    [JsonIgnore]
    public PropertyInfo Property { get; init; } = null!;

    /// <summary>
    /// Gets the type of the target, see <see cref="TargetType"/>.
    /// </summary>
    [JsonIgnore]
    public Type? TargetClrType { get; init; }
}
