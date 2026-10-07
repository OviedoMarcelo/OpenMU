// <copyright file="SeasonProgress.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Progression;

/// <summary>
/// The experience which an account gained in the pass of a season.
/// </summary>
public class SeasonProgress
{
    /// <summary>
    /// Gets or sets the identifier of the account.
    /// </summary>
    public Guid AccountId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the season, as configured in the plugin configuration.
    /// </summary>
    public string SeasonId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the experience of the pass.
    /// </summary>
    public long Experience { get; set; }
}
