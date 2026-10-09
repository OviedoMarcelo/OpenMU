// <copyright file="EnumValue.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AdminApi.Configuration;

/// <summary>
/// A value of an enumeration.
/// </summary>
/// <param name="Name">The name.</param>
/// <param name="Value">The numeric value.</param>
/// <param name="Caption">The caption.</param>
public record EnumValue(string Name, long Value, string Caption);
