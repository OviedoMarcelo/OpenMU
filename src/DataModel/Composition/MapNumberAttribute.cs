// <copyright file="MapNumberAttribute.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.DataModel.Composition;

/// <summary>
/// Marks a property which holds the number of a map, so that the UI offers the maps by their name.
/// It's meant for configurations which are stored by value, like the ones of plugins, and can't reference the map itself.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class MapNumberAttribute : Attribute
{
}
