// <copyright file="SeasonPremium.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Progression;

/// <summary>
/// The premium track of the season pass, which has been activated for an account.
/// </summary>
public class SeasonPremium
{
    /// <summary>
    /// Gets or sets the identifier of the account.
    /// </summary>
    public Guid AccountId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the season.
    /// </summary>
    public string SeasonId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp (UTC) when the premium track has been activated.
    /// </summary>
    public DateTime GrantedAt { get; set; }

    /// <summary>
    /// Gets or sets who activated it, e.g. the name of a game master or "shop".
    /// </summary>
    public string? GrantedBy { get; set; }
}
