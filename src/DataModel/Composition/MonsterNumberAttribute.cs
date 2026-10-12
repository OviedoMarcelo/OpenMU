// <copyright file="MonsterNumberAttribute.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.DataModel.Composition;

/// <summary>
/// Marks a property which holds the number of a monster, so that the UI offers the monsters by their name.
/// It's meant for configurations which are stored by value, like the ones of plugins, and can't reference the monster itself.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class MonsterNumberAttribute : Attribute
{
}
