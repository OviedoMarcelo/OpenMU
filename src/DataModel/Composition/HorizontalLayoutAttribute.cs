// <copyright file="HorizontalLayoutAttribute.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.DataModel.Composition;

/// <summary>
/// Marks a small type, whose fields are shown side by side on the UI when its objects are edited in a list.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class HorizontalLayoutAttribute : Attribute
{
}
