// <copyright file="HelpText.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AdminApi.Configuration;

/// <summary>
/// A help text of a property.
/// </summary>
/// <param name="Info">What the property means.</param>
/// <param name="Impact">The impact of a change on the game, if any.</param>
public record HelpText(string Info, string? Impact);
