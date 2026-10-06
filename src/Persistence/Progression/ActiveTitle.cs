// <copyright file="ActiveTitle.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Progression;

/// <summary>
/// The title which a character shows below its name.
/// </summary>
public class ActiveTitle
{
    /// <summary>
    /// Gets or sets the identifier of the character.
    /// </summary>
    public Guid CharacterId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the title, as configured in the plugin configuration.
    /// </summary>
    public string TitleId { get; set; } = string.Empty;
}
