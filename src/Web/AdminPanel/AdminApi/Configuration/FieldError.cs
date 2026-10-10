// <copyright file="FieldError.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AdminApi.Configuration;

/// <summary>
/// An invalid value which was sent to the admin API.
/// </summary>
/// <param name="Path">The path of the value, e.g. <c>Attributes[2].Value</c>.</param>
/// <param name="Message">The message, in Spanish like the admin frontend.</param>
public record FieldError(string Path, string Message);
