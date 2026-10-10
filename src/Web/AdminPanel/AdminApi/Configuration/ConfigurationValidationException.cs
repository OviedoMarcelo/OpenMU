// <copyright file="ConfigurationValidationException.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AdminApi.Configuration;

/// <summary>
/// Thrown when values which were sent to the admin API are invalid. Nothing has been saved then.
/// </summary>
public class ConfigurationValidationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationValidationException"/> class.
    /// </summary>
    /// <param name="errors">The errors.</param>
    public ConfigurationValidationException(IReadOnlyList<FieldError> errors)
        : base(string.Join(Environment.NewLine, errors.Select(e => $"{e.Path}: {e.Message}")))
    {
        this.Errors = errors;
    }

    /// <summary>
    /// Gets the errors.
    /// </summary>
    public IReadOnlyList<FieldError> Errors { get; }
}
