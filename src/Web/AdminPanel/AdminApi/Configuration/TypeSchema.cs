// <copyright file="TypeSchema.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AdminApi.Configuration;

/// <summary>
/// Describes a configuration type for the admin frontend.
/// </summary>
/// <param name="Name">The name of the type, which identifies it in the API.</param>
/// <param name="Caption">The caption.</param>
/// <param name="Description">The description.</param>
/// <param name="IsBrowsable">If set to <c>true</c>, the objects of the type can be listed (they're a collection of the game configuration).</param>
/// <param name="NameProperty">The property which holds the name of an object, if any.</param>
/// <param name="ListColumns">The properties which are shown as columns of a list of objects of this type.</param>
/// <param name="Properties">The properties.</param>
public record TypeSchema(
    string Name,
    string Caption,
    string? Description,
    bool IsBrowsable,
    string? NameProperty,
    IReadOnlyList<string> ListColumns,
    IReadOnlyList<PropertySchema> Properties);
