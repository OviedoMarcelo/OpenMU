// <copyright file="KeySource.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AdminApi.Configuration;

/// <summary>
/// The objects whose keys a text property (or a list of texts) can hold, see <see cref="DataModel.Composition.KeyOfAttribute"/>.
/// </summary>
/// <param name="Collection">The name of the collection property of the root object.</param>
/// <param name="Key">The name of the property of the objects which holds the key.</param>
/// <param name="Caption">The name of the property of the objects which is shown.</param>
/// <param name="PlugIn">The full type name of the plugin whose configuration has the collection; <c>null</c> for the root object.</param>
public record KeySource(string Collection, string Key, string Caption, string? PlugIn = null);
