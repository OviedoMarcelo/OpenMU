// <copyright file="ConfigurationPropertyFilter.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Shared;

using System.ComponentModel;
using System.Reflection;
using MUnique.OpenMU.DataModel.Composition;

/// <summary>
/// Decides which properties of a data model type are shown to an administrator.
/// Used by the generated forms of the admin panel and by the admin API, so both show the same fields.
/// </summary>
public static class ConfigurationPropertyFilter
{
    /// <summary>
    /// Gets the public instance properties of the type which are shown to an administrator.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>The visible properties, in declaration order.</returns>
    public static IEnumerable<PropertyInfo> GetVisibleProperties(Type type)
    {
        return type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy)
            .Where(IsVisible);
    }

    /// <summary>
    /// Determines whether the property is shown to an administrator.
    /// </summary>
    /// <param name="property">The property.</param>
    /// <returns><c>true</c>, if the property is shown.</returns>
    /// <remarks>
    /// Transient properties aren't persisted, and the "Raw" and "Joined" properties are helpers of
    /// the persistence layer for the actual properties.
    /// </remarks>
    public static bool IsVisible(PropertyInfo property)
    {
        return property.GetCustomAttribute<TransientAttribute>() is null
               && (property.GetCustomAttribute<BrowsableAttribute>()?.Browsable ?? true)
               && !property.Name.StartsWith("Raw", StringComparison.Ordinal)
               && !property.Name.StartsWith("Joined", StringComparison.Ordinal)
               && property.GetIndexParameters().Length == 0;
    }
}
